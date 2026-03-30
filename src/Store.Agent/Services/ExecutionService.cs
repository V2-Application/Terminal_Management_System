using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.Extensions.Logging;
using Store.Agent.Execution;
using Store.Agent.Models;
using Store.Agent.Security;
using Store.Agent.Services;   // LocalStateRepository
using System.IO.Compression;  // ZipFile
using System.Net.Http.Json;   // PostAsJsonAsync, ReadFromJsonAsync

namespace Store.Agent.Services;

/// <summary>
/// Orchestrates the full command execution pipeline:
///   Pre-flight → Download+Verify package → Backup DLLs
///   → Billing Lock → Notify start → Execute script
///   → Report result → Billing Unlock (SUCCESS only)
///
/// SAFETY: Billing lock is NEVER released on failure.
/// Lock is held until HO sends explicit ROLLBACK or UNLOCK command.
/// </summary>
public class ExecutionService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AgentConfig _config;
    private readonly PreFlightChecker _preFlight;
    private readonly PackageHashVerifier _hashVerifier;
    private readonly ScriptExecutor _executor;
    private readonly BillingLockService _lockService;
    private readonly LocalStateRepository _localState;
    private readonly ILogger<ExecutionService> _logger;

    public ExecutionService(
        IHttpClientFactory httpFactory,
        AgentConfig config,
        PreFlightChecker preFlight,
        PackageHashVerifier hashVerifier,
        ScriptExecutor executor,
        BillingLockService lockService,
        LocalStateRepository localState,
        ILogger<ExecutionService> logger)
    {
        _httpFactory  = httpFactory;
        _config       = config;
        _preFlight    = preFlight;
        _hashVerifier = hashVerifier;
        _executor     = executor;
        _lockService  = lockService;
        _localState   = localState;
        _logger       = logger;
    }

    public async Task ExecuteAsync(
        PendingCommandDto command, Guid terminalId, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("HoApi");
        _logger.LogInformation(
            "Starting command {Id} type={Type}", command.CommandId, command.CommandType);

        // [1] Pre-flight checks (disk, directory writable, not duplicate)
        var pf = _preFlight.RunAll(command.CommandId);
        if (!pf.Passed)
        {
            await SubmitResultAsync(http, command.CommandId, terminalId, -1,
                string.Empty, "Pre-flight FAILED: " + string.Join("; ", pf.FailureReasons), 0, ct);
            return;
        }

        // [2] Download + verify package
        string? extractDir = null;
        if (command.PackageId.HasValue)
        {
            extractDir = await DownloadAndVerifyAsync(http, command, ct);
            if (extractDir == null)
            {
                await SubmitResultAsync(http, command.CommandId, terminalId, -2,
                    string.Empty, "Package download or hash verification failed", 0, ct);
                return;
            }
        }

        // [3] Backup existing DLLs before touching anything
        BackupDlls(command.CommandId);

        // [4] Lock billing (graceful POS close)
        var locked = await _lockService.LockAsync(ct);
        if (!locked)
        {
            await SubmitResultAsync(http, command.CommandId, terminalId, -3,
                string.Empty, "Billing lock failed — POS process could not be stopped", 0, ct);
            return;
        }

        // [5] Notify HO that execution has started
        try
        {
            await http.PostAsJsonAsync("executions/start",
                new ExecutionStartRequest
                {
                    CommandId  = command.CommandId,
                    TerminalId = terminalId,
                    StartedAt  = DateTime.UtcNow
                }, ct);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not notify HO of start — continuing anyway"); }

        // [6] Find and run the script
        var scriptPath = FindScript(extractDir, command.CommandType);
        if (scriptPath == null)
        {
            await SubmitResultAsync(http, command.CommandId, terminalId, -4,
                string.Empty, $"Script not found for command type '{command.CommandType}'", 0, ct);
            return;
        }

        var result = await _executor.RunAsync(
            scriptPath,
            Path.GetDirectoryName(scriptPath)!,
            _config.ExecutionTimeoutSeconds, ct);

        // [7] Record in local SQLite for idempotency + offline caching
        await _localState.RecordExecutionAsync(command.CommandId, command.CommandNonce,
            command.CommandType, result);

        // [8] Submit result to HO
        await SubmitResultAsync(http, command.CommandId, terminalId,
            result.ExitCode, result.Stdout, result.Stderr, result.DurationMs, ct);

        // [9] Unlock billing ONLY on success
        if (result.ExitCode == 0)
        {
            _logger.LogInformation("Command {Id} SUCCEEDED — unlocking billing", command.CommandId);
            await _lockService.UnlockAsync(ct);
        }
        else
        {
            _logger.LogError(
                "Command {Id} FAILED (exit={Exit}) — billing lock HELD. " +
                "HO must issue ROLLBACK or UNLOCK to release.",
                command.CommandId, result.ExitCode);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private async Task<string?> DownloadAndVerifyAsync(
        HttpClient http, PendingCommandDto command, CancellationToken ct)
    {
        var cacheDir = Path.Combine(_config.LocalPackageCacheDir, command.PackageId!.Value.ToString());
        Directory.CreateDirectory(cacheDir);
        var zipPath = Path.Combine(cacheDir, "package.zip");

        try
        {
            var resp = await http.GetAsync($"packages/{command.PackageId}/download", ct);
            resp.EnsureSuccessStatusCode();
            await using var fs = File.Create(zipPath);
            await resp.Content.CopyToAsync(fs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Package download failed for {PkgId}", command.PackageId);
            return null;
        }

        if (!await _hashVerifier.VerifyAsync(zipPath, command.PackageChecksum ?? string.Empty))
        {
            _logger.LogError("Package hash MISMATCH — rejecting package {PkgId}", command.PackageId);
            return null;
        }

        var extractDir = Path.Combine(cacheDir, "extracted");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);
        _logger.LogInformation("Package extracted to {Dir}", extractDir);
        return extractDir;
    }

    private static string? FindScript(string? dir, string commandType)
    {
        if (string.IsNullOrEmpty(dir)) return null;

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FyClose",        "FY-CLOSE.BAT" },
            { "NsoSetup",       "IT_NSO_BATCH.BAT" },
            { "TimeSyncTest",   "Time_set_before_Test_Bil.bat" },
            { "TimeSyncProd",   "Time_set_After_Billing.bat" },
        };

        if (!map.TryGetValue(commandType, out var scriptName)) return null;
        var fullPath = Path.Combine(dir, scriptName);
        return File.Exists(fullPath) ? fullPath : null;
    }

    private void BackupDlls(Guid commandId)
    {
        var backupDir = Path.Combine(Path.GetTempPath(), "FYBackup", commandId.ToString());
        try
        {
            Directory.CreateDirectory(backupDir);
            if (!Directory.Exists(_config.PosExtensionsPath)) return;
            foreach (var dll in Directory.GetFiles(_config.PosExtensionsPath, "*.dll"))
                File.Copy(dll, Path.Combine(backupDir, Path.GetFileName(dll)), overwrite: true);
            _logger.LogInformation("DLLs backed up to {Dir}", backupDir);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "DLL backup failed — proceeding"); }
    }

    private async Task SubmitResultAsync(
        HttpClient http, Guid commandId, Guid terminalId,
        int exitCode, string stdout, string stderr, long durationMs, CancellationToken ct)
    {
        try
        {
            await http.PostAsJsonAsync("executions/result", new ExecutionResultRequest
            {
                CommandId   = commandId,
                TerminalId  = terminalId,
                ExitCode    = exitCode,
                Stdout      = Trim(stdout, 65000),
                Stderr      = Trim(stderr, 65000),
                DurationMs  = durationMs,
                CompletedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to submit result for {CmdId} — result cached in local SQLite for replay on reconnect",
                commandId);
        }
    }

    private static string Trim(string s, int max)
        => s.Length > max ? s[^max..] : s;
}

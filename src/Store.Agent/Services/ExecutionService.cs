using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Store.Agent.Execution;
using Store.Agent.Models;
using Store.Agent.Security;
using System.IO.Compression;
using System.Net.Http.Json;

namespace Store.Agent.Services;

/// <summary>
/// Orchestrates the full command execution pipeline:
/// Pre-flight → Download → Verify → Backup → Lock → Execute → Report → Unlock
/// </summary>
public class ExecutionService
{
    private readonly HttpClient _httpClient;
    private readonly AgentConfig _config;
    private readonly PreFlightChecker _preFlight;
    private readonly PackageHashVerifier _hashVerifier;
    private readonly ScriptExecutor _executor;
    private readonly BillingLockService _lockService;
    private readonly LocalStateRepository _localState;
    private readonly ILogger<ExecutionService> _logger;

    public ExecutionService(
        HttpClient httpClient, AgentConfig config,
        PreFlightChecker preFlight, PackageHashVerifier hashVerifier,
        ScriptExecutor executor, BillingLockService lockService,
        LocalStateRepository localState, ILogger<ExecutionService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _preFlight = preFlight;
        _hashVerifier = hashVerifier;
        _executor = executor;
        _lockService = lockService;
        _localState = localState;
        _logger = logger;
    }

    public async Task ExecuteAsync(PendingCommandDto command, Guid terminalId, CancellationToken ct)
    {
        _logger.LogInformation("ExecutionService: Starting command {CommandId} ({Type})",
            command.CommandId, command.CommandType);

        // [1] Pre-flight checks
        var preFlightResult = _preFlight.RunAll(command.CommandId);
        if (!preFlightResult.Passed)
        {
            await SubmitResultAsync(command.CommandId, terminalId, -1,
                string.Empty, $"Pre-flight FAILED: {string.Join("; ", preFlightResult.FailureReasons)}",
                0, ct);
            return;
        }

        // [2] Download + verify package
        string? packagePath = null;
        if (command.PackageId.HasValue)
        {
            packagePath = await DownloadAndVerifyPackageAsync(command, ct);
            if (packagePath == null)
            {
                await SubmitResultAsync(command.CommandId, terminalId, -2,
                    string.Empty, "Package download or verification failed", 0, ct);
                return;
            }
        }

        // [3] Backup existing DLLs
        var backupDir = Path.Combine(Path.GetTempPath(), "FYBackup", command.CommandId.ToString());
        BackupExistingDlls(backupDir);

        // [4] Billing lock
        var locked = await _lockService.LockAsync(ct);
        if (!locked)
        {
            await SubmitResultAsync(command.CommandId, terminalId, -3,
                string.Empty, "Failed to acquire billing lock — POS could not be stopped", 0, ct);
            return;
        }

        // [5] Signal execution start
        await _httpClient.PostAsJsonAsync($"executions/start",
            new ExecutionStartRequest { CommandId = command.CommandId, TerminalId = terminalId, StartedAt = DateTime.UtcNow }, ct);

        // [6] Execute script
        var scriptPath = packagePath != null
            ? FindScriptInPackage(packagePath, command.CommandType)
            : null;

        if (scriptPath == null)
        {
            await SubmitResultAsync(command.CommandId, terminalId, -4,
                string.Empty, "Script not found in package", 0, ct);
            return;
        }

        var executionResult = await _executor.RunAsync(
            scriptPath, Path.GetDirectoryName(scriptPath)!,
            _config.ExecutionTimeoutSeconds, ct);

        // [7] Save to local SQLite (for offline result caching)
        await _localState.RecordExecutionAsync(command.CommandId, command.CommandNonce,
            command.CommandType, executionResult);

        // [8] Submit result to HO
        await SubmitResultAsync(command.CommandId, terminalId, executionResult.ExitCode,
            executionResult.Stdout, executionResult.Stderr, executionResult.DurationMs, ct);

        // [9] Unlock billing ONLY on success
        if (executionResult.ExitCode == 0)
        {
            _logger.LogInformation("Command {CommandId} SUCCESS — unlocking billing", command.CommandId);
            await _lockService.UnlockAsync(ct);
        }
        else
        {
            _logger.LogError("Command {CommandId} FAILED (exit={ExitCode}) — HOLDING billing lock",
                command.CommandId, executionResult.ExitCode);
            // Lock held until HO sends explicit UNLOCK or ROLLBACK command
        }
    }

    private async Task<string?> DownloadAndVerifyPackageAsync(PendingCommandDto command, CancellationToken ct)
    {
        var cacheDir = Path.Combine(_config.LocalPackageCacheDir, command.PackageId.ToString()!);
        Directory.CreateDirectory(cacheDir);
        var zipPath = Path.Combine(cacheDir, "package.zip");

        try
        {
            var response = await _httpClient.GetAsync($"packages/{command.PackageId}/download", ct);
            response.EnsureSuccessStatusCode();
            await using var fs = File.Create(zipPath);
            await response.Content.CopyToAsync(fs, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Package download failed");
            return null;
        }

        if (!await _hashVerifier.VerifyAsync(zipPath, command.PackageChecksum ?? string.Empty))
        {
            _logger.LogError("Package hash verification FAILED — aborting");
            return null;
        }

        // Extract ZIP
        var extractDir = Path.Combine(cacheDir, "extracted");
        if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
        ZipFile.ExtractToDirectory(zipPath, extractDir);
        return extractDir;
    }

    private string? FindScriptInPackage(string packageDir, string commandType)
    {
        var scriptMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "FyClose", "FY-CLOSE.BAT" },
            { "NsoSetup", "IT_NSO_BATCH.BAT" },
            { "TimeSyncTest", "Time_set_before_Test_Bil.bat" },
            { "TimeSyncProd", "Time_set_After_Billing.bat" },
        };

        if (!scriptMap.TryGetValue(commandType, out var scriptName)) return null;
        var path = Path.Combine(packageDir, scriptName);
        return File.Exists(path) ? path : null;
    }

    private void BackupExistingDlls(string backupDir)
    {
        try
        {
            Directory.CreateDirectory(backupDir);
            if (Directory.Exists(_config.PosExtensionsPath))
            {
                foreach (var dll in Directory.GetFiles(_config.PosExtensionsPath, "*.dll"))
                {
                    File.Copy(dll, Path.Combine(backupDir, Path.GetFileName(dll)), overwrite: true);
                }
                _logger.LogInformation("DLL backup created in {BackupDir}", backupDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DLL backup failed — proceeding anyway");
        }
    }

    private async Task SubmitResultAsync(Guid commandId, Guid terminalId, int exitCode,
        string stdout, string stderr, long durationMs, CancellationToken ct)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("executions/result", new ExecutionResultRequest
            {
                CommandId = commandId,
                TerminalId = terminalId,
                ExitCode = exitCode,
                Stdout = stdout.Length > 65535 ? stdout[^65535..] : stdout,
                Stderr = stderr.Length > 65535 ? stderr[^65535..] : stderr,
                DurationMs = durationMs,
                CompletedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit result for command {CommandId} — cached locally", commandId);
        }
    }
}

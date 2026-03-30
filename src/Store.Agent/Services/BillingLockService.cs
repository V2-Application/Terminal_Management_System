using Store.Agent.Models;
using System.Diagnostics;

namespace Store.Agent.Services;

/// <summary>
/// Manages POS billing lock/unlock.
/// SAFETY: Lock is never released on failure — only on explicit SUCCESS or ROLLBACK.
/// </summary>
public class BillingLockService
{
    private readonly AgentConfig _config;
    private readonly ILogger<BillingLockService> _logger;
    private const int GracefulWaitMs = 30_000;
    private const int ForcefulWaitMs = 60_000;

    public BillingLockService(AgentConfig config, ILogger<BillingLockService> logger)
    {
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Gracefully closes POS, then force-kills if needed.
    /// Returns true if POS is confirmed stopped.
    /// </summary>
    public async Task<bool> LockAsync(CancellationToken ct)
    {
        _logger.LogInformation("BILLING LOCK: Requesting POS shutdown");

        var processes = Process.GetProcessesByName("POS");
        if (processes.Length == 0)
        {
            _logger.LogInformation("POS.exe not running — billing lock trivially acquired");
            return true;
        }

        // Graceful close
        foreach (var p in processes)
        {
            try { p.CloseMainWindow(); } catch { /* no main window */ }
        }

        await Task.Delay(GracefulWaitMs, ct);

        // Check if still running
        var remaining = Process.GetProcessesByName("POS");
        if (remaining.Length == 0)
        {
            _logger.LogInformation("POS.exe gracefully stopped");
            return true;
        }

        _logger.LogWarning("POS.exe still running after {Wait}s — sending KILL", GracefulWaitMs / 1000);
        foreach (var p in remaining)
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to kill POS.exe (pid {Pid})", p.Id);
            }
        }

        await Task.Delay(5000, ct);
        var stillRunning = Process.GetProcessesByName("POS").Length > 0;
        if (stillRunning) _logger.LogError("POS.exe CANNOT be terminated — billing lock FAILED");
        return !stillRunning;
    }

    /// <summary>
    /// Restarts POS after successful execution.
    /// Only called on SUCCESS or ROLLBACK — NEVER on failure.
    /// </summary>
    public async Task<bool> UnlockAsync(CancellationToken ct)
    {
        _logger.LogInformation("BILLING UNLOCK: Starting POS.exe");

        if (!File.Exists(_config.PosExecutablePath))
        {
            _logger.LogError("POS executable not found: {Path}", _config.PosExecutablePath);
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _config.PosExecutablePath,
            UseShellExecute = true
        });

        // Wait up to 2 minutes for POS to start
        for (int i = 0; i < 24; i++)
        {
            await Task.Delay(5000, ct);
            if (Process.GetProcessesByName("POS").Length > 0)
            {
                _logger.LogInformation("POS.exe started successfully");
                return true;
            }
        }

        _logger.LogError("POS.exe did not start within 120s after unlock attempt");
        return false;
    }
}

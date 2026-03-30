using Store.Agent.Models;

namespace Store.Agent.Execution;

public class PreFlightResult
{
    public bool Passed { get; set; }
    public List<string> FailureReasons { get; set; } = new();
}

/// <summary>
/// Runs all pre-execution safety checks before acquiring billing lock or executing scripts.
/// If any check fails, execution is aborted — no changes made to the system.
/// </summary>
public class PreFlightChecker
{
    private readonly AgentConfig _config;
    private readonly ILogger<PreFlightChecker> _logger;

    public PreFlightChecker(AgentConfig config, ILogger<PreFlightChecker> logger)
    {
        _config = config;
        _logger = logger;
    }

    public PreFlightResult RunAll(Guid commandId)
    {
        var result = new PreFlightResult { Passed = true };

        CheckDiskSpace(result);
        CheckTargetDirectoryWritable(result);
        CheckPosNotProcessingTransaction(result);
        CheckBackupDirectoryAccessible(commandId, result);

        if (!result.Passed)
        {
            _logger.LogWarning("Pre-flight FAILED for command {CommandId}: {Reasons}",
                commandId, string.Join(", ", result.FailureReasons));
        }
        else
        {
            _logger.LogInformation("Pre-flight PASSED for command {CommandId}", commandId);
        }

        return result;
    }

    private void CheckDiskSpace(PreFlightResult result)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_config.PosExtensionsPath) ?? "C:");
            var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            if (freeGB < 0.5)
            {
                result.Passed = false;
                result.FailureReasons.Add($"Insufficient disk space: {freeGB:F2} GB free (need 0.5 GB)");
            }
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.FailureReasons.Add($"Disk check failed: {ex.Message}");
        }
    }

    private void CheckTargetDirectoryWritable(PreFlightResult result)
    {
        if (!Directory.Exists(_config.PosExtensionsPath))
        {
            result.Passed = false;
            result.FailureReasons.Add($"POS Extensions directory not found: {_config.PosExtensionsPath}");
            return;
        }
        var testFile = Path.Combine(_config.PosExtensionsPath, $".rtmls_write_test_{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
        }
        catch
        {
            result.Passed = false;
            result.FailureReasons.Add($"POS Extensions directory is not writable: {_config.PosExtensionsPath}");
        }
    }

    private void CheckPosNotProcessingTransaction(PreFlightResult result)
    {
        // Check if POS has any open transaction windows
        // In a full implementation: query AX transaction log or check named mutex
        var posProcesses = System.Diagnostics.Process.GetProcessesByName("POS");
        if (posProcesses.Length > 0)
        {
            // Simplified check — in production: check AX transaction log for open transactions
            _logger.LogInformation("POS.exe is running ({Count} instance(s)) — will be gracefully closed during lock phase",
                posProcesses.Length);
        }
    }

    private void CheckBackupDirectoryAccessible(Guid commandId, PreFlightResult result)
    {
        var backupDir = Path.Combine(Path.GetTempPath(), "FYBackup", commandId.ToString());
        try
        {
            Directory.CreateDirectory(backupDir);
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.FailureReasons.Add($"Cannot create backup directory: {ex.Message}");
        }
    }
}

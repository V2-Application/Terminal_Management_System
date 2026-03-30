using System.Diagnostics;
using System.Text;

namespace Store.Agent.Execution;

public class ExecutionResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public bool TimedOut { get; set; }
}

/// <summary>
/// Safely wraps cmd.exe /c execution of batch scripts.
/// Captures full stdout + stderr. Never uses ShellExecute=true.
/// </summary>
public class ScriptExecutor
{
    private readonly ILogger<ScriptExecutor> _logger;

    public ScriptExecutor(ILogger<ScriptExecutor> logger) => _logger = logger;

    public async Task<ExecutionResult> RunAsync(
        string scriptPath,
        string workingDirectory,
        int timeoutSeconds,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Executing script: {Script}", scriptPath);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var sw = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,        // SECURITY: must be false
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            // SECURITY: NO credentials passed here — script must not need them
        };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdout.AppendLine(e.Data);
                _logger.LogDebug("[STDOUT] {Line}", e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderr.AppendLine(e.Data);
                _logger.LogDebug("[STDERR] {Line}", e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var completed = await Task.Run(() =>
            process.WaitForExit(TimeSpan.FromSeconds(timeoutSeconds)), ct);

        if (!completed)
        {
            _logger.LogWarning("Script timed out after {Timeout}s — killing process", timeoutSeconds);
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
        }

        sw.Stop();
        var result = new ExecutionResult
        {
            ExitCode = completed ? process.ExitCode : -1,
            Stdout = stdout.ToString(),
            Stderr = stderr.ToString(),
            DurationMs = sw.ElapsedMilliseconds,
            TimedOut = !completed
        };

        _logger.LogInformation("Script finished. ExitCode={ExitCode} Duration={Duration}ms",
            result.ExitCode, result.DurationMs);
        return result;
    }
}

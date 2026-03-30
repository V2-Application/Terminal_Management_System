namespace Store.Agent.Models;

/// <summary>
/// SQLite record for tracking local command execution state.
/// Provides idempotency and offline result caching.
/// </summary>
public class LocalExecutionRecord
{
    public int Id { get; set; }
    public Guid CommandId { get; set; }
    public Guid CommandNonce { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public int? ExitCode { get; set; }
    public string? CachedStdout { get; set; }
    public string? CachedStderr { get; set; }
    public long? DurationMs { get; set; }
    public bool ResultSubmitted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

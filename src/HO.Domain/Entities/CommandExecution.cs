namespace HO.Domain.Entities;

public class CommandExecution
{
    public Guid ExecutionId { get; set; } = Guid.NewGuid();
    public Guid CommandId { get; set; }
    public Guid TerminalId { get; set; }
    public int AttemptNumber { get; set; } = 1;
    public int? ExitCode { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public long? DurationMs { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Command Command { get; set; } = null!;
}

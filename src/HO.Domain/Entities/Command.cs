using HO.Domain.Enums;

namespace HO.Domain.Entities;

public class Command
{
    public Guid CommandId { get; set; } = Guid.NewGuid();
    public Guid TerminalId { get; set; }
    public Guid StoreId { get; set; }
    public Guid? FYJobId { get; set; }
    public CommandType CommandType { get; set; }
    public Guid CommandNonce { get; set; } = Guid.NewGuid();
    public Guid? PackageId { get; set; }
    public CommandStatus Status { get; set; } = CommandStatus.Queued;
    public int Priority { get; set; } = 5;
    public int TTLMinutes { get; set; } = 240;
    public DateTime ScheduledFor { get; set; } = DateTime.UtcNow;
    public DateTime? DispatchedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime? RetryAfter { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "SYSTEM";

    public Terminal Terminal { get; set; } = null!;
    public ICollection<CommandExecution> Executions { get; set; } = new List<CommandExecution>();
}

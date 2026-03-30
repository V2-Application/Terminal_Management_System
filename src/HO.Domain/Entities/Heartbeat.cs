namespace HO.Domain.Entities;

public class Heartbeat
{
    public long HeartbeatId { get; set; }
    public Guid TerminalId { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? AgentVersion { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public decimal? DiskFreeGB { get; set; }
    public bool PosProcessRunning { get; set; }
    public DateTime? LocalTime { get; set; }

    public Terminal Terminal { get; set; } = null!;
}

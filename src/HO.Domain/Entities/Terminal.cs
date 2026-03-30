using HO.Domain.Enums;

namespace HO.Domain.Entities;

public class Terminal
{
    public Guid TerminalId { get; set; } = Guid.NewGuid();
    public Guid StoreId { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;  // BIOS GUID
    public string MachineName { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? OsVersion { get; set; }
    public string? AgentVersion { get; set; }
    public string? PosVersion { get; set; }
    public TerminalStatus Status { get; set; } = TerminalStatus.Unregistered;
    public bool IsPrimary { get; set; }
    public DateTime? LastHeartbeat { get; set; }
    public decimal? DiskFreeGB { get; set; }
    public string? AuthTokenHash { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Store Store { get; set; } = null!;
    public ICollection<Command> Commands { get; set; } = new List<Command>();
}

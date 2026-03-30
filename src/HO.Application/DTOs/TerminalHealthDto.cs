namespace HO.Application.DTOs;

public class TerminalHealthDto
{
    public Guid TerminalId { get; set; }
    public string TerminalCode { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastHeartbeat { get; set; }
    public string? AgentVersion { get; set; }
    public decimal? DiskFreeGB { get; set; }
    public bool IsPrimary { get; set; }
    public TimeSpan? TimeSinceLastHeartbeat =>
        LastHeartbeat.HasValue ? DateTime.UtcNow - LastHeartbeat.Value : null;
}

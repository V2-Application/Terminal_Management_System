namespace HO.Domain.Entities;

public class AuditLog
{
    public long AuditId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string Result { get; set; } = "SUCCESS";
    public string? Details { get; set; }
}

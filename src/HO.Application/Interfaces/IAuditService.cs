namespace HO.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string userId, string action, string entityType, string entityId,
        string? oldValue = null, string? newValue = null, string result = "SUCCESS",
        string? details = null, string? ipAddress = null);
}

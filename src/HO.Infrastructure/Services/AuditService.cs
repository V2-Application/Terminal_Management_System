using HO.Application.Interfaces;
using HO.Domain.Entities;
using HO.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AppDbContext db, ILogger<AuditService> logger)
    {
        _db = db; _logger = logger;
    }

    public async Task LogAsync(
        string userId, string action, string entityType, string entityId,
        string? oldValue = null, string? newValue = null,
        string result = "SUCCESS", string? details = null, string? ipAddress = null)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Timestamp  = DateTime.UtcNow,
                UserId     = userId,
                IpAddress  = ipAddress,
                Action     = action,
                EntityType = entityType,
                EntityId   = entityId,
                OldValue   = oldValue,
                NewValue   = newValue,
                Result     = result,
                Details    = details
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit log failures must never break the main flow
            _logger.LogError(ex, "Failed to write audit log: {Action} {EntityType}/{EntityId}",
                action, entityType, entityId);
        }
    }
}

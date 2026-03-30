using HO.Application.Interfaces;
using HO.Domain.DomainEvents;

namespace HO.Infrastructure.SignalR;

/// <summary>
/// No-op ISignalRService used in contexts where SignalR hub is not mounted
/// (e.g., HO.API background request processing, HO.Worker).
/// All methods are safe to call — they simply do nothing.
/// </summary>
public class NullSignalRService : ISignalRService
{
    public Task BroadcastTerminalStatusChanged(TerminalStatusChangedEvent evt)
        => Task.CompletedTask;

    public Task BroadcastCommandStatusUpdated(CommandStatusUpdatedEvent evt)
        => Task.CompletedTask;

    public Task BroadcastFYJobProgress(FYJobProgressEvent evt)
        => Task.CompletedTask;

    public Task BroadcastStoreWentOffline(Guid storeId, string storeName, DateTime lastSeen)
        => Task.CompletedTask;

    public Task BroadcastAlertCreated(string alertId, string severity, string storeId, string message)
        => Task.CompletedTask;
}

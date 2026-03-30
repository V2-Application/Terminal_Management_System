using HO.Domain.DomainEvents;

namespace HO.Application.Interfaces;

public interface ISignalRService
{
    Task BroadcastTerminalStatusChanged(TerminalStatusChangedEvent evt);
    Task BroadcastCommandStatusUpdated(CommandStatusUpdatedEvent evt);
    Task BroadcastFYJobProgress(FYJobProgressEvent evt);
    Task BroadcastStoreWentOffline(Guid storeId, string storeName, DateTime lastSeen);
    Task BroadcastAlertCreated(string alertId, string severity, string storeId, string message);
}

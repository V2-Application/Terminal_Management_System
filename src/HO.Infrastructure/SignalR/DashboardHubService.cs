using HO.Application.Interfaces;
using HO.Domain.DomainEvents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.SignalR;

/// <summary>
/// Sends real-time SignalR events to connected HO browser clients.
/// Uses IHubContext{DashboardHub} — both types are in HO.Infrastructure, no circular deps.
/// </summary>
public class DashboardHubService : ISignalRService
{
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<DashboardHubService> _logger;

    public DashboardHubService(
        IHubContext<DashboardHub> hub,
        ILogger<DashboardHubService> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    public async Task BroadcastTerminalStatusChanged(TerminalStatusChangedEvent evt)
    {
        await _hub.Clients.All.SendAsync("TerminalStatusChanged", evt);
        _logger.LogDebug("SignalR >> TerminalStatusChanged {Id} -> {Status}",
            evt.TerminalId, evt.NewStatus);
    }

    public async Task BroadcastCommandStatusUpdated(CommandStatusUpdatedEvent evt)
    {
        // Broadcast to all + targeted store group
        await _hub.Clients.All.SendAsync("CommandStatusUpdated", evt);
        await _hub.Clients.Group("store_" + evt.StoreId)
                           .SendAsync("CommandStatusUpdated", evt);
    }

    public async Task BroadcastFYJobProgress(FYJobProgressEvent evt)
        => await _hub.Clients.All.SendAsync("FYJobProgress", evt);

    public async Task BroadcastStoreWentOffline(Guid storeId, string storeName, DateTime lastSeen)
        => await _hub.Clients.All.SendAsync("StoreWentOffline",
               new { storeId, storeName, lastSeen });

    public async Task BroadcastAlertCreated(
        string alertId, string severity, string storeId, string message)
        => await _hub.Clients.All.SendAsync("AlertCreated",
               new { alertId, severity, storeId, message });
}

using HO.Application.Interfaces;
using HO.Domain.DomainEvents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.SignalR;

/// <summary>
/// Sends SignalR events from background services (Hangfire jobs, command processing)
/// to connected HO browser clients via IHubContext — no direct Hub reference needed.
/// The actual DashboardHub class lives in HO.Web.Hubs.
/// </summary>
public class DashboardHubService : ISignalRService
{
    private readonly IHubContext _hub;
    private readonly ILogger<DashboardHubService> _logger;

    // Use the non-generic IHubContext to avoid coupling to HO.Web assembly
    public DashboardHubService(IHubContext hub, ILogger<DashboardHubService> logger)
    {
        _hub    = hub;
        _logger = logger;
    }

    public async Task BroadcastTerminalStatusChanged(TerminalStatusChangedEvent evt)
    {
        await _hub.Clients.All.SendCoreAsync("TerminalStatusChanged", new object[] { evt });
        _logger.LogDebug("SignalR: TerminalStatusChanged {Id} -> {Status}", evt.TerminalId, evt.NewStatus);
    }

    public async Task BroadcastCommandStatusUpdated(CommandStatusUpdatedEvent evt)
    {
        await _hub.Clients.All.SendCoreAsync("CommandStatusUpdated", new object[] { evt });
        await _hub.Clients.Group("store_" + evt.StoreId)
            .SendCoreAsync("CommandStatusUpdated", new object[] { evt });
    }

    public async Task BroadcastFYJobProgress(FYJobProgressEvent evt)
        => await _hub.Clients.All.SendCoreAsync("FYJobProgress", new object[] { evt });

    public async Task BroadcastStoreWentOffline(Guid storeId, string storeName, DateTime lastSeen)
        => await _hub.Clients.All.SendCoreAsync("StoreWentOffline",
            new object[] { new { storeId, storeName, lastSeen } });

    public async Task BroadcastAlertCreated(string alertId, string severity, string storeId, string message)
        => await _hub.Clients.All.SendCoreAsync("AlertCreated",
            new object[] { new { alertId, severity, storeId, message } });
}

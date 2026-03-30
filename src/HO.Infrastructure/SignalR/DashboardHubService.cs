using HO.Application.Interfaces;
using HO.Domain.DomainEvents;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.SignalR;

// Hub definition lives in HO.Web — this service uses IHubContext to push from background jobs
public class DashboardHubService : ISignalRService
{
    private readonly IHubContext<DashboardHub> _hub;
    private readonly ILogger<DashboardHubService> _logger;

    public DashboardHubService(IHubContext<DashboardHub> hub, ILogger<DashboardHubService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task BroadcastTerminalStatusChanged(TerminalStatusChangedEvent evt)
    {
        await _hub.Clients.All.SendAsync("TerminalStatusChanged", evt);
        _logger.LogDebug("SignalR: TerminalStatusChanged {TerminalId} -> {NewStatus}", evt.TerminalId, evt.NewStatus);
    }

    public async Task BroadcastCommandStatusUpdated(CommandStatusUpdatedEvent evt)
    {
        await _hub.Clients.Group($"store_{evt.StoreId}").SendAsync("CommandStatusUpdated", evt);
        await _hub.Clients.All.SendAsync("CommandStatusUpdated", evt);
    }

    public async Task BroadcastFYJobProgress(FYJobProgressEvent evt)
    {
        await _hub.Clients.All.SendAsync("FYJobProgress", evt);
    }

    public async Task BroadcastStoreWentOffline(Guid storeId, string storeName, DateTime lastSeen)
    {
        await _hub.Clients.All.SendAsync("StoreWentOffline", new { storeId, storeName, lastSeen });
    }

    public async Task BroadcastAlertCreated(string alertId, string severity, string storeId, string message)
    {
        await _hub.Clients.All.SendAsync("AlertCreated", new { alertId, severity, storeId, message });
    }
}

// Placeholder — actual hub in HO.Web
public class DashboardHub : Hub
{
    public async Task JoinStoreGroup(string storeId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"store_{storeId}");

    public async Task JoinFYJobGroup(string fyJobId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"fyjob_{fyJobId}");

    public async Task JoinRegionGroup(string region)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"region_{region}");
}

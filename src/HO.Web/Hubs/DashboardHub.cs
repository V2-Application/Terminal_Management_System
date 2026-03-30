using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HO.Web.Hubs;

/// <summary>
/// SignalR hub for real-time HO dashboard updates.
/// Only HO browser clients connect here — store agents use REST polling.
/// Groups: store_{storeId}, region_{region}, fyjob_{fyJobId}
/// </summary>
[Authorize]
public class DashboardHub : Hub
{
    private readonly ILogger<DashboardHub> _logger;

    public DashboardHub(ILogger<DashboardHub> logger) => _logger = logger;

    public async Task JoinStoreGroup(string storeId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"store_{storeId}");
        _logger.LogDebug("User {User} joined store group {StoreId}", Context.UserIdentifier, storeId);
    }

    public async Task LeaveStoreGroup(string storeId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"store_{storeId}");

    public async Task JoinFYJobGroup(string fyJobId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"fyjob_{fyJobId}");

    public async Task JoinRegionGroup(string region)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"region_{region}");

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Dashboard client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Dashboard client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

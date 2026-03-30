using HO.Application.Interfaces;
using HO.Domain.DomainEvents;
using HO.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Jobs;

public class HeartbeatMonitorJob
{
    private readonly ITerminalRepository _terminalRepo;
    private readonly ISignalRService _signalR;
    private readonly INotificationService _notifications;
    private readonly ILogger<HeartbeatMonitorJob> _logger;

    private static readonly TimeSpan OfflineThreshold = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AlertThreshold = TimeSpan.FromMinutes(15);

    public HeartbeatMonitorJob(
        ITerminalRepository terminalRepo,
        ISignalRService signalR,
        INotificationService notifications,
        ILogger<HeartbeatMonitorJob> logger)
    {
        _terminalRepo = terminalRepo;
        _signalR = signalR;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("HeartbeatMonitorJob: Checking terminal heartbeats");

        var offlineTerminals = await _terminalRepo.GetOfflineTerminalsAsync(OfflineThreshold, ct);

        foreach (var terminal in offlineTerminals)
        {
            var wasActive = terminal.Status == TerminalStatus.Active;
            terminal.Status = TerminalStatus.Offline;
            terminal.UpdatedAt = DateTime.UtcNow;
            await _terminalRepo.UpdateAsync(terminal, ct);

            if (wasActive)
            {
                _logger.LogWarning("Terminal {TerminalId} ({StoreCode}) went OFFLINE. Last heartbeat: {LastHB}",
                    terminal.TerminalId, terminal.Store?.StoreCode, terminal.LastHeartbeat);

                await _signalR.BroadcastTerminalStatusChanged(new TerminalStatusChangedEvent(
                    terminal.TerminalId, terminal.StoreId, "Active", "Offline", DateTime.UtcNow));

                await _signalR.BroadcastStoreWentOffline(
                    terminal.StoreId, terminal.Store?.StoreName ?? string.Empty, terminal.LastHeartbeat ?? DateTime.UtcNow);

                if (terminal.LastHeartbeat < DateTime.UtcNow - AlertThreshold)
                {
                    await _notifications.SendAlertAsync(
                        $"Store Offline: {terminal.Store?.StoreName}",
                        $"Terminal {terminal.TerminalCode} at {terminal.Store?.StoreName} has been offline since {terminal.LastHeartbeat:yyyy-MM-dd HH:mm} UTC.",
                        "WARNING");
                }
            }
        }

        _logger.LogInformation("HeartbeatMonitorJob: Processed {Count} offline terminals", offlineTerminals.Count());
    }
}

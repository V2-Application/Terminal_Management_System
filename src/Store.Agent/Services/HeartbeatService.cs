using HO.Contracts.Requests;
using Store.Agent.Models;
using System.Net.Http.Json;

namespace Store.Agent.Services;

/// <summary>
/// Sends heartbeat to HO API every HeartbeatIntervalSeconds.
/// Includes disk space, POS process status, agent version.
/// </summary>
public class HeartbeatService
{
    private readonly HttpClient _httpClient;
    private readonly AgentConfig _config;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(HttpClient httpClient, AgentConfig config, ILogger<HeartbeatService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(Guid terminalId, CancellationToken ct)
    {
        try
        {
            var posRunning = System.Diagnostics.Process.GetProcessesByName("POS").Length > 0;
            var drive = new DriveInfo(Path.GetPathRoot(_config.PosExtensionsPath) ?? "C:");
            var diskFreeGB = (decimal)(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024));

            await _httpClient.PostAsJsonAsync("heartbeat", new HeartbeatRequest
            {
                TerminalId = terminalId,
                Status = "ACTIVE",
                DiskFreeGB = diskFreeGB,
                PosProcessRunning = posRunning,
                AgentVersion = GetAgentVersion(),
                LocalTime = DateTime.Now
            }, ct);

            _logger.LogDebug("Heartbeat sent. Disk:{Disk:F1}GB POS:{PosRunning}", diskFreeGB, posRunning);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat failed — will retry next interval");
        }
    }

    private static string GetAgentVersion()
        => System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
}

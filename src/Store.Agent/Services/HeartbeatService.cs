using HO.Contracts.Requests;
using Microsoft.Extensions.Logging;
using Store.Agent.Models;
using System.Net.Http.Json;   // PostAsJsonAsync

namespace Store.Agent.Services;

/// <summary>
/// Sends heartbeat to HO API every HeartbeatIntervalSeconds.
/// Heartbeat includes: disk space, POS process status, agent version.
/// If heartbeat fails, the agent continues running — HO will mark it offline after threshold.
/// </summary>
public class HeartbeatService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AgentConfig _config;
    private readonly ILogger<HeartbeatService> _logger;

    public HeartbeatService(
        IHttpClientFactory httpFactory,
        AgentConfig config,
        ILogger<HeartbeatService> logger)
    {
        _httpFactory = httpFactory;
        _config      = config;
        _logger      = logger;
    }

    public async Task SendAsync(Guid terminalId, CancellationToken ct)
    {
        try
        {
            var posRunning  = IsPosRunning();
            var diskFreeGB  = GetDiskFreeGB();
            var agentVersion = GetVersion();

            var http = _httpFactory.CreateClient("HoApi");

            await http.PostAsJsonAsync("heartbeat", new HeartbeatRequest
            {
                TerminalId       = terminalId,
                Status           = "ACTIVE",
                DiskFreeGB       = diskFreeGB,
                PosProcessRunning = posRunning,
                AgentVersion     = agentVersion,
                LocalTime        = DateTime.Now
            }, ct);

            _logger.LogDebug(
                "Heartbeat sent. Disk={Disk:F1}GB POS={PosRunning} v{Version}",
                diskFreeGB, posRunning, agentVersion);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Heartbeat failed — will retry next interval (HO marks offline after 10 min)");
        }
    }

    private static bool IsPosRunning()
    {
        try { return System.Diagnostics.Process.GetProcessesByName("POS").Length > 0; }
        catch { return false; }
    }

    private decimal GetDiskFreeGB()
    {
        try
        {
            var root  = Path.GetPathRoot(_config.PosExtensionsPath) ?? "C:";
            var drive = new DriveInfo(root);
            return (decimal)(drive.AvailableFreeSpace / (1024.0 * 1024 * 1024));
        }
        catch { return 0m; }
    }

    private static string GetVersion()
        => System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";
}

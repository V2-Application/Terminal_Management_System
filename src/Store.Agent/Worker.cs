using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Store.Agent.Models;
using Store.Agent.Security;
using Store.Agent.Services;
using System.Net.Http.Json;

namespace Store.Agent;

/// <summary>
/// Main Windows Service worker.
/// On startup: ensures terminal is registered with HO (first-time or re-register).
/// Then runs two PeriodicTimers concurrently:
///   - Heartbeat every HeartbeatIntervalSeconds
///   - Command poll every PollIntervalSeconds
/// </summary>
public class Worker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly AgentConfig _config;
    private readonly ILogger<Worker> _logger;
    private Guid _terminalId = Guid.Empty;

    public Worker(IServiceProvider services, AgentConfig config, ILogger<Worker> logger)
    {
        _services = services;
        _config   = config;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "RetailTMS Store Agent v{Version} starting. Store: {StoreCode}",
            GetVersion(), _config.StoreCode);

        // Ensure terminal is registered with HO
        _terminalId = await EnsureRegisteredAsync(stoppingToken);
        if (_terminalId == Guid.Empty)
        {
            _logger.LogCritical("Terminal registration failed — agent cannot operate. Check HoApiBaseUrl and StoreCode.");
            return;
        }

        _logger.LogInformation("Agent active. TerminalId={TerminalId}", _terminalId);

        // Run heartbeat + poll loops concurrently
        var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds));
        var pollTimer      = new PeriodicTimer(TimeSpan.FromSeconds(_config.PollIntervalSeconds));

        // Send an immediate heartbeat so HO shows the terminal as ACTIVE right away
        await SendHeartbeatAsync(stoppingToken);

        var heartbeatTask = RunHeartbeatLoopAsync(heartbeatTimer, stoppingToken);
        var pollTask      = RunPollLoopAsync(pollTimer, stoppingToken);

        await Task.WhenAll(heartbeatTask, pollTask);

        _logger.LogInformation("RetailTMS Store Agent stopped.");
    }

    private async Task RunHeartbeatLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
            await SendHeartbeatAsync(ct);
    }

    private async Task RunPollLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = _services.CreateScope();
            var poller = scope.ServiceProvider.GetRequiredService<CommandPollerService>();
            await poller.PollAsync(_terminalId, ct);
        }
    }

    private async Task SendHeartbeatAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var heartbeat = scope.ServiceProvider.GetRequiredService<HeartbeatService>();
        await heartbeat.SendAsync(_terminalId, ct);
    }

    private async Task<Guid> EnsureRegisteredAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var credStore = scope.ServiceProvider.GetRequiredService<CredentialStore>();

        // Load existing credentials
        var creds = await credStore.LoadAsync();
        if (creds?.IsRegistered == true && creds.TerminalId != Guid.Empty)
        {
            _logger.LogInformation("Existing credentials loaded. TerminalId={TerminalId}", creds.TerminalId);
            return creds.TerminalId;
        }

        // First-time registration
        _logger.LogInformation("No credentials found — registering with HO API at {Url}", _config.HoApiBaseUrl);

        var httpFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var http = httpFactory.CreateClient("HoApi");

        try
        {
            var registerResponse = await http.PostAsJsonAsync("terminals/register", new
            {
                storeCode   = _config.StoreCode,
                machineId   = GetMachineId(),
                machineName = Environment.MachineName,
                osVersion   = Environment.OSVersion.VersionString,
                agentVersion = GetVersion(),
            }, ct);

            if (!registerResponse.IsSuccessStatusCode)
            {
                var body = await registerResponse.Content.ReadAsStringAsync(ct);
                _logger.LogError("Registration failed {Status}: {Body}", registerResponse.StatusCode, body);
                return Guid.Empty;
            }

            var result = await registerResponse.Content
                .ReadFromJsonAsync<HO.Contracts.Responses.RegisterTerminalResponse>(ct);

            if (result == null)
            {
                _logger.LogError("Registration response was empty");
                return Guid.Empty;
            }

            await credStore.SaveAsync(new AgentCredentials
            {
                TerminalId   = result.TerminalId,
                AuthToken    = result.AuthToken,
                RefreshToken = result.RefreshToken,
                TokenExpiry  = result.TokenExpiry,
                IsRegistered = true
            });

            _logger.LogInformation("Terminal registered successfully. TerminalId={Id}", result.TerminalId);
            return result.TerminalId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during registration");
            return Guid.Empty;
        }
    }

    private static string GetMachineId()
    {
        // Try BIOS UUID (best hardware fingerprint on Windows)
        try
        {
            using var searcher = new System.Management.ManagementObjectSearcher(
                "SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (System.Management.ManagementObject obj in searcher.Get())
            {
                var uuid = obj["UUID"]?.ToString();
                if (!string.IsNullOrWhiteSpace(uuid) && uuid != "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")
                    return uuid;
            }
        }
        catch (Exception)
        {
            // WMI unavailable — fall back to machine name + env combo
        }
        return $"{Environment.MachineName}-{Environment.ProcessorCount}";
    }

    private static string GetVersion()
        => System.Reflection.Assembly
            .GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";
}

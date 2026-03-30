using Store.Agent.Models;
using Store.Agent.Security;
using Store.Agent.Services;
using System.Net.Http.Json;

namespace Store.Agent;

/// <summary>
/// Main Windows Service worker.
/// Coordinates registration, heartbeat timer, and command poll timer.
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
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RetailTMS Store Agent starting up. Store: {StoreCode}", _config.StoreCode);

        // Ensure registered
        _terminalId = await EnsureRegisteredAsync(stoppingToken);
        if (_terminalId == Guid.Empty)
        {
            _logger.LogCritical("Agent registration failed — cannot proceed.");
            return;
        }

        _logger.LogInformation("Agent registered. TerminalId={TerminalId}", _terminalId);

        using var heartbeatTimer = new PeriodicTimer(TimeSpan.FromSeconds(_config.HeartbeatIntervalSeconds));
        using var pollTimer = new PeriodicTimer(TimeSpan.FromSeconds(_config.PollIntervalSeconds));

        // Run both timers concurrently
        var heartbeatTask = RunHeartbeatLoopAsync(heartbeatTimer, stoppingToken);
        var pollTask = RunPollLoopAsync(pollTimer, stoppingToken);

        await Task.WhenAll(heartbeatTask, pollTask);
        _logger.LogInformation("RetailTMS Store Agent stopped.");
    }

    private async Task RunHeartbeatLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = _services.CreateScope();
            var heartbeat = scope.ServiceProvider.GetRequiredService<HeartbeatService>();
            await heartbeat.SendAsync(_terminalId, ct);
        }
    }

    private async Task RunPollLoopAsync(PeriodicTimer timer, CancellationToken ct)
    {
        // Send one heartbeat immediately on start
        using (var scope = _services.CreateScope())
        {
            var heartbeat = scope.ServiceProvider.GetRequiredService<HeartbeatService>();
            await heartbeat.SendAsync(_terminalId, ct);
        }

        while (await timer.WaitForNextTickAsync(ct))
        {
            using var scope = _services.CreateScope();
            var poller = scope.ServiceProvider.GetRequiredService<CommandPollerService>();
            await poller.PollAsync(_terminalId, ct);
        }
    }

    private async Task<Guid> EnsureRegisteredAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var credStore = scope.ServiceProvider.GetRequiredService<CredentialStore>();
        var credentials = await credStore.LoadAsync();

        if (credentials?.IsRegistered == true && credentials.TerminalId != Guid.Empty)
        {
            _logger.LogInformation("Loaded existing credentials. TerminalId={TerminalId}", credentials.TerminalId);
            return credentials.TerminalId;
        }

        _logger.LogInformation("No credentials found — registering terminal with HO");
        var httpClient = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("HoApi");

        var machineId = GetMachineId();
        var response = await httpClient.PostAsJsonAsync("terminals/register", new
        {
            storeCode = _config.StoreCode,
            machineId,
            machineName = Environment.MachineName,
            osVersion = Environment.OSVersion.VersionString,
            agentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Registration failed: {Status}", response.StatusCode);
            return Guid.Empty;
        }

        var result = await response.Content.ReadFromJsonAsync<HO.Contracts.Responses.RegisterTerminalResponse>(ct);
        if (result == null) return Guid.Empty;

        await credStore.SaveAsync(new AgentCredentials
        {
            TerminalId = result.TerminalId,
            AuthToken = result.AuthToken,
            RefreshToken = result.RefreshToken,
            TokenExpiry = result.TokenExpiry,
            IsRegistered = true
        });

        return result.TerminalId;
    }

    private static string GetMachineId()
    {
        // Use BIOS GUID as machine fingerprint
        try
        {
            var searcher = new System.Management.ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct");
            foreach (var obj in searcher.Get())
                return obj["UUID"]?.ToString() ?? Environment.MachineName;
        }
        catch { /* fall back */ }
        return Environment.MachineName;
    }
}

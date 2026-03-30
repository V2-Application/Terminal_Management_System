using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.DomainEvents;
using HO.Domain.Entities;
using HO.Domain.Enums;
using HO.Infrastructure.Security;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Services;

public class TerminalService : ITerminalService
{
    private readonly ITerminalRepository _terminals;
    private readonly IStoreRepository    _stores;
    private readonly IAuditService       _audit;
    private readonly ISignalRService     _signalR;
    private readonly JwtService          _jwt;
    private readonly ILogger<TerminalService> _logger;

    public TerminalService(
        ITerminalRepository terminals, IStoreRepository stores,
        IAuditService audit, ISignalRService signalR,
        JwtService jwt, ILogger<TerminalService> logger)
    {
        _terminals = terminals; _stores = stores;
        _audit = audit; _signalR = signalR;
        _jwt = jwt; _logger = logger;
    }

    public async Task<(Guid TerminalId, string AuthToken, string RefreshToken)> RegisterAsync(
        string storeCode, string machineId, string machineName,
        string osVersion, string agentVersion, string? posPath,
        string? ipAddress, CancellationToken ct = default)
    {
        var store = await _stores.GetByCodeAsync(storeCode, ct)
            ?? throw new KeyNotFoundException($"Store '{storeCode}' not found.");

        // Return existing registration if machine already registered
        var existing = await _terminals.GetByMachineIdAsync(machineId, ct);
        if (existing != null)
        {
            _logger.LogInformation("Re-registering existing terminal {Id}", existing.TerminalId);
            existing.AgentVersion = agentVersion;
            existing.IpAddress    = ipAddress;
            existing.Status       = TerminalStatus.Registered;
            existing.UpdatedAt    = DateTime.UtcNow;
            await _terminals.UpdateAsync(existing, ct);

            var (tok, _) = _jwt.GenerateTerminalToken(existing.TerminalId, storeCode);
            var refresh   = _jwt.GenerateRefreshToken();
            existing.AuthTokenHash = _jwt.HashToken(tok);
            await _terminals.UpdateAsync(existing, ct);
            return (existing.TerminalId, tok, refresh);
        }

        // Count existing terminals for this store
        var storeTerminals = await _terminals.GetByStoreAsync(store.StoreId, ct);
        var termNum        = storeTerminals.Count() + 1;

        var terminal = new Terminal
        {
            StoreId      = store.StoreId,
            TerminalCode = $"{storeCode}-T{termNum:D2}",
            MachineId    = machineId,
            MachineName  = machineName,
            IpAddress    = ipAddress,
            OsVersion    = osVersion,
            AgentVersion = agentVersion,
            Status       = TerminalStatus.Registered,
            IsPrimary    = termNum == 1,
        };

        await _terminals.AddAsync(terminal, ct);

        var (authToken, _) = _jwt.GenerateTerminalToken(terminal.TerminalId, storeCode);
        var refreshToken    = _jwt.GenerateRefreshToken();
        terminal.AuthTokenHash = _jwt.HashToken(authToken);
        await _terminals.UpdateAsync(terminal, ct);

        await _audit.LogAsync("SYSTEM", "TERMINAL_REGISTERED", "Terminal",
            terminal.TerminalId.ToString(), details: $"Store={storeCode} Machine={machineName}");

        _logger.LogInformation("New terminal registered: {Code} for store {Store}",
            terminal.TerminalCode, storeCode);

        return (terminal.TerminalId, authToken, refreshToken);
    }

    public async Task<(string AuthToken, DateTime Expiry)> RefreshTokenAsync(
        Guid terminalId, string refreshToken, CancellationToken ct = default)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct)
            ?? throw new KeyNotFoundException($"Terminal {terminalId} not found.");

        var (token, expiry) = _jwt.GenerateTerminalToken(
            terminalId, terminal.Store.StoreCode);
        terminal.AuthTokenHash = _jwt.HashToken(token);
        terminal.TokenExpiry   = expiry;
        await _terminals.UpdateAsync(terminal, ct);

        return (token, expiry);
    }

    public async Task RecordHeartbeatAsync(
        Guid terminalId, string status, decimal? diskFreeGB,
        bool posRunning, string? agentVersion, DateTime localTime,
        CancellationToken ct = default)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct);
        if (terminal == null)
        {
            _logger.LogWarning("Heartbeat from unknown terminal {Id}", terminalId);
            return;
        }

        var wasOffline = terminal.Status == TerminalStatus.Offline;

        terminal.LastHeartbeat = DateTime.UtcNow;
        terminal.DiskFreeGB    = diskFreeGB;
        terminal.AgentVersion  = agentVersion ?? terminal.AgentVersion;

        if (terminal.Status == TerminalStatus.Offline ||
            terminal.Status == TerminalStatus.Registered)
            terminal.Status = TerminalStatus.Active;

        await _terminals.UpdateAsync(terminal, ct);

        if (wasOffline)
        {
            _logger.LogInformation("Terminal {Id} came back ONLINE", terminalId);
            await _signalR.BroadcastTerminalStatusChanged(new TerminalStatusChangedEvent(
                terminalId, terminal.StoreId, "Offline", "Active", DateTime.UtcNow));
        }
    }

    public async Task SetStatusAsync(Guid terminalId, string status, CancellationToken ct = default)
    {
        var terminal = await _terminals.GetByIdAsync(terminalId, ct);
        if (terminal == null) return;
        if (Enum.TryParse<TerminalStatus>(status, true, out var s))
        {
            var old = terminal.Status.ToString();
            terminal.Status = s;
            await _terminals.UpdateAsync(terminal, ct);
            await _signalR.BroadcastTerminalStatusChanged(
                new TerminalStatusChangedEvent(terminalId, terminal.StoreId, old, status, DateTime.UtcNow));
        }
    }
}

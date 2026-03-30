using HO.Domain.Entities;

namespace HO.Application.Services;

public interface ITerminalService
{
    Task<(Guid TerminalId, string AuthToken, string RefreshToken)> RegisterAsync(
        string storeCode, string machineId, string machineName,
        string osVersion, string agentVersion, string? posPath,
        string? ipAddress, CancellationToken ct = default);

    Task<(string AuthToken, DateTime Expiry)> RefreshTokenAsync(
        Guid terminalId, string refreshToken, CancellationToken ct = default);

    Task RecordHeartbeatAsync(Guid terminalId, string status,
        decimal? diskFreeGB, bool posRunning, string? agentVersion,
        DateTime localTime, CancellationToken ct = default);

    Task SetStatusAsync(Guid terminalId, string status, CancellationToken ct = default);
}

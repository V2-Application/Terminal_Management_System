using HO.Application.Services;
using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/terminals")]
public class TerminalsController : ControllerBase
{
    private readonly ITerminalService _terminalService;
    private readonly ILogger<TerminalsController> _logger;

    public TerminalsController(ITerminalService terminalService, ILogger<TerminalsController> logger)
    {
        _terminalService = terminalService;
        _logger = logger;
    }

    /// <summary>
    /// One-time terminal registration. Called by Store.Agent on first startup.
    /// IP must be in allowed range (configured in middleware).
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<RegisterTerminalResponse>> Register(
        [FromBody] RegisterTerminalRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.StoreCode) || string.IsNullOrWhiteSpace(request.MachineId))
            return BadRequest(new { error = "StoreCode and MachineId are required." });

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        _logger.LogInformation("Terminal registration request from {IP} for store {StoreCode}", ipAddress, request.StoreCode);

        var (terminalId, authToken, refreshToken) = await _terminalService.RegisterAsync(
            request.StoreCode, request.MachineId, request.MachineName,
            request.OsVersion, request.AgentVersion, request.PosPath, ipAddress, ct);

        return CreatedAtAction(nameof(Register), new RegisterTerminalResponse
        {
            TerminalId = terminalId,
            AuthToken = authToken,
            RefreshToken = refreshToken,
            TokenExpiry = DateTime.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Refresh expired auth token using refresh token.
    /// </summary>
    [HttpPost("auth/refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> RefreshToken(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var (token, expiry) = await _terminalService.RefreshTokenAsync(request.TerminalId, request.RefreshToken, ct);
        return Ok(new TokenResponse { AuthToken = token, Expiry = expiry });
    }
}

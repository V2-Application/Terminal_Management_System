using HO.Application.Services;
using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/heartbeat")]
[Authorize(Policy = "TerminalPolicy")]
public class HeartbeatController : ControllerBase
{
    private readonly ITerminalService _terminalService;

    public HeartbeatController(ITerminalService terminalService)
        => _terminalService = terminalService;

    /// <summary>
    /// Agent heartbeat — called every 5 minutes by Store.Agent.
    /// Updates terminal last-seen, disk info, POS status.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HeartbeatResponse>> Submit(
        [FromBody] HeartbeatRequest request, CancellationToken ct)
    {
        await _terminalService.RecordHeartbeatAsync(
            request.TerminalId, request.Status, request.DiskFreeGB,
            request.PosProcessRunning, request.AgentVersion, request.LocalTime, ct);

        return Ok(new HeartbeatResponse
        {
            ServerTime = DateTime.UtcNow
        });
    }
}

using HO.Application.Services;
using HO.Contracts.Requests;
using HO.Contracts.Responses;
using HO.Infrastructure.Persistence;
using HO.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/heartbeat")]
[Authorize(Policy = "TerminalPolicy")]
public class HeartbeatController : ControllerBase
{
    private readonly ITerminalService _terminalService;
    private readonly AppDbContext     _db;

    public HeartbeatController(ITerminalService terminalService, AppDbContext db)
    {
        _terminalService = terminalService;
        _db = db;
    }

    /// <summary>
    /// Agent heartbeat — called every 5 minutes by Store.Agent.
    /// Updates terminal status and persists heartbeat record.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<HeartbeatResponse>> Submit(
        [FromBody] HeartbeatRequest request, CancellationToken ct)
    {
        await _terminalService.RecordHeartbeatAsync(
            request.TerminalId, request.Status, request.DiskFreeGB,
            request.PosProcessRunning, request.AgentVersion, request.LocalTime, ct);

        // Also persist heartbeat record for history
        _db.Heartbeats.Add(new Heartbeat
        {
            TerminalId        = request.TerminalId,
            ReceivedAt        = DateTime.UtcNow,
            AgentVersion      = request.AgentVersion,
            Status            = request.Status,
            DiskFreeGB        = request.DiskFreeGB,
            PosProcessRunning = request.PosProcessRunning,
            LocalTime         = request.LocalTime
        });
        await _db.SaveChangesAsync(ct);

        return Ok(new HeartbeatResponse
        {
            ServerTime = DateTime.UtcNow
        });
    }
}

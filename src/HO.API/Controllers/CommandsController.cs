using HO.Application.Services;
using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/commands")]
[Authorize(Policy = "TerminalPolicy")]
public class CommandsController : ControllerBase
{
    private readonly ICommandService _commandService;
    private readonly ILogger<CommandsController> _logger;

    public CommandsController(ICommandService commandService, ILogger<CommandsController> logger)
    {
        _commandService = commandService;
        _logger = logger;
    }

    /// <summary>
    /// Agent polls this every 60 seconds to get queued commands.
    /// Returns only QUEUED commands for this terminal.
    /// Idempotent — safe to call repeatedly.
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<PendingCommandsResponse>> GetPending(
        [FromQuery] Guid terminalId, CancellationToken ct)
    {
        var commands = await _commandService.GetPendingCommandsAsync(terminalId, ct);

        var response = new PendingCommandsResponse
        {
            Commands = commands.Select(c => new PendingCommandDto
            {
                CommandId = c.CommandId,
                CommandType = c.CommandType.ToString(),
                PackageId = c.PackageId,
                TtlMinutes = c.TTLMinutes,
                ScheduledFor = c.ScheduledFor,
                CommandNonce = c.CommandNonce
            }).ToList()
        };

        return Ok(response);
    }

    /// <summary>
    /// Agent sends ACK when it has received and is about to execute a command.
    /// Idempotent — double ACK returns 200 without error.
    /// </summary>
    [HttpPost("{commandId:guid}/ack")]
    public async Task<ActionResult<CommandAckResponse>> Acknowledge(
        Guid commandId, [FromBody] CommandAckRequest request, CancellationToken ct)
    {
        await _commandService.AcknowledgeCommandAsync(commandId, request.TerminalId, ct);
        return Ok(new CommandAckResponse { Acknowledged = true });
    }
}

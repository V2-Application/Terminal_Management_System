using HO.Application.Services;
using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HO.API.Controllers;

[ApiController]
[Route("api/v1/executions")]
[Authorize(Policy = "TerminalPolicy")]
public class ExecutionsController : ControllerBase
{
    private readonly ICommandService _commandService;

    public ExecutionsController(ICommandService commandService)
        => _commandService = commandService;

    /// <summary>
    /// Agent calls this when it starts executing a command (after pre-flight).
    /// Sets command status to RUNNING. Triggers SignalR broadcast.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] ExecutionStartRequest request, CancellationToken ct)
    {
        await _commandService.RecordExecutionStartAsync(request.CommandId, request.TerminalId, ct);
        return Ok();
    }

    /// <summary>
    /// Agent submits final result (exitCode, stdout, stderr, duration).
    /// If exitCode != 0, command marked FAILED and retry evaluation begins.
    /// Idempotent — duplicate submission returns 200.
    /// </summary>
    [HttpPost("result")]
    public async Task<ActionResult<ExecutionResultResponse>> SubmitResult(
        [FromBody] ExecutionResultRequest request, CancellationToken ct)
    {
        await _commandService.RecordExecutionResultAsync(
            request.CommandId, request.TerminalId, request.ExitCode,
            request.Stdout, request.Stderr, request.DurationMs, ct);

        return Created(string.Empty, new ExecutionResultResponse
        {
            ResultId = Guid.NewGuid(),
            RetryScheduled = request.ExitCode != 0
        });
    }
}

using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Microsoft.Extensions.Logging;
using Store.Agent.Models;
using Store.Agent.Services;   // LocalStateRepository
using System.Net.Http.Json;   // PostAsJsonAsync, GetFromJsonAsync

namespace Store.Agent.Services;

/// <summary>
/// Polls HO API every PollIntervalSeconds for pending commands.
/// Processes commands sequentially (MaxConcurrentCommands = 1).
/// Idempotent: checks local SQLite nonce cache before executing.
/// </summary>
public class CommandPollerService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AgentConfig _config;
    private readonly ExecutionService _executionService;
    private readonly LocalStateRepository _localState;
    private readonly ILogger<CommandPollerService> _logger;

    public CommandPollerService(
        IHttpClientFactory httpFactory,
        AgentConfig config,
        ExecutionService executionService,
        LocalStateRepository localState,
        ILogger<CommandPollerService> logger)
    {
        _httpFactory      = httpFactory;
        _config           = config;
        _executionService = executionService;
        _localState       = localState;
        _logger           = logger;
    }

    public async Task PollAsync(Guid terminalId, CancellationToken ct)
    {
        try
        {
            var http = _httpFactory.CreateClient("HoApi");

            var response = await http.GetFromJsonAsync<PendingCommandsResponse>(
                $"commands/pending?terminalId={terminalId}", ct);

            if (response?.Commands == null || response.Commands.Count == 0)
            {
                _logger.LogDebug("No pending commands for terminal {Id}", terminalId);
                return;
            }

            _logger.LogInformation("Received {N} pending command(s)", response.Commands.Count);

            foreach (var cmd in response.Commands)
            {
                ct.ThrowIfCancellationRequested();

                // Idempotency guard — skip if nonce already executed locally
                if (await _localState.IsAlreadyExecutedAsync(cmd.CommandNonce))
                {
                    _logger.LogWarning(
                        "Skipping command {CmdId} — nonce {Nonce} already executed locally",
                        cmd.CommandId, cmd.CommandNonce);
                    await AcknowledgeAsync(http, cmd.CommandId, terminalId, ct);
                    continue;
                }

                await AcknowledgeAsync(http, cmd.CommandId, terminalId, ct);
                await _executionService.ExecuteAsync(cmd, terminalId, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error during poll — will retry next interval");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during poll");
        }
    }

    private async Task AcknowledgeAsync(
        HttpClient http, Guid commandId, Guid terminalId, CancellationToken ct)
    {
        try
        {
            await http.PostAsJsonAsync($"commands/{commandId}/ack",
                new CommandAckRequest
                {
                    CommandId  = commandId,
                    TerminalId = terminalId,
                    ReceivedAt = DateTime.UtcNow
                }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to ACK command {CmdId} — HO will re-queue it if not ACKed within TTL",
                commandId);
        }
    }
}

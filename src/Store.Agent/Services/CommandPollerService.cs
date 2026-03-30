using HO.Contracts.Requests;
using HO.Contracts.Responses;
using Store.Agent.Models;
using Store.Agent.Execution;
using System.Net.Http.Json;

namespace Store.Agent.Services;

/// <summary>
/// Core polling service — runs on background timer every PollIntervalSeconds.
/// Calls GET /api/v1/commands/pending, processes each received command sequentially.
/// </summary>
public class CommandPollerService
{
    private readonly HttpClient _httpClient;
    private readonly AgentConfig _config;
    private readonly ExecutionService _executionService;
    private readonly LocalStateRepository _localState;
    private readonly ILogger<CommandPollerService> _logger;

    public CommandPollerService(
        HttpClient httpClient,
        AgentConfig config,
        ExecutionService executionService,
        LocalStateRepository localState,
        ILogger<CommandPollerService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _executionService = executionService;
        _localState = localState;
        _logger = logger;
    }

    public async Task PollAsync(Guid terminalId, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Polling for commands. TerminalId={TerminalId}", terminalId);

            var response = await _httpClient.GetFromJsonAsync<PendingCommandsResponse>(
                $"commands/pending?terminalId={terminalId}", ct);

            if (response?.Commands == null || !response.Commands.Any())
            {
                _logger.LogDebug("No pending commands.");
                return;
            }

            _logger.LogInformation("Received {Count} pending command(s)", response.Commands.Count);

            foreach (var cmd in response.Commands)
            {
                ct.ThrowIfCancellationRequested();

                // Idempotency: check if already executed
                if (await _localState.IsAlreadyExecutedAsync(cmd.CommandNonce))
                {
                    _logger.LogWarning("Skipping duplicate command {CommandId} (nonce already executed)", cmd.CommandId);
                    // Still send ACK so server knows we have it
                    await AcknowledgeAsync(cmd.CommandId, terminalId, ct);
                    continue;
                }

                await AcknowledgeAsync(cmd.CommandId, terminalId, ct);
                await _executionService.ExecuteAsync(cmd, terminalId, ct);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during command poll");
        }
    }

    private async Task AcknowledgeAsync(Guid commandId, Guid terminalId, CancellationToken ct)
    {
        try
        {
            await _httpClient.PostAsJsonAsync($"commands/{commandId}/ack",
                new CommandAckRequest { CommandId = commandId, TerminalId = terminalId, ReceivedAt = DateTime.UtcNow }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ACK command {CommandId} — will retry on reconnect", commandId);
        }
    }
}

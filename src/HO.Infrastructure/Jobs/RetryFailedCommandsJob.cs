using HO.Application.Interfaces;
using HO.Application.Services;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Jobs;

public class RetryFailedCommandsJob
{
    private readonly ICommandRepository _commandRepo;
    private readonly ICommandService _commandService;
    private readonly ILogger<RetryFailedCommandsJob> _logger;

    public RetryFailedCommandsJob(
        ICommandRepository commandRepo,
        ICommandService commandService,
        ILogger<RetryFailedCommandsJob> logger)
    {
        _commandRepo = commandRepo;
        _commandService = commandService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        var retryable = await _commandRepo.GetFailedForRetryAsync(DateTime.UtcNow, ct);

        foreach (var cmd in retryable)
        {
            if (cmd.RetryCount >= cmd.MaxRetries) continue;

            _logger.LogInformation("RetryFailedCommandsJob: Retrying command {CommandId} (attempt {N})",
                cmd.CommandId, cmd.RetryCount + 1);

            await _commandService.RetryCommandAsync(cmd.CommandId, ct);
        }
    }
}

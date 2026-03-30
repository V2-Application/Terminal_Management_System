using HO.Application.Interfaces;
using HO.Application.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Jobs;

public class FYCloseOrchestratorJob
{
    private readonly IFYJobRepository _fyJobRepo;
    private readonly IFYCloseService _fyCloseService;
    private readonly ILogger<FYCloseOrchestratorJob> _logger;

    public FYCloseOrchestratorJob(
        IFYJobRepository fyJobRepo,
        IFYCloseService fyCloseService,
        ILogger<FYCloseOrchestratorJob> logger)
    {
        _fyJobRepo = fyJobRepo;
        _fyCloseService = fyCloseService;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 0)]  // No Hangfire auto-retry for orchestrator
    public async Task StartOrchestration(Guid fyJobId, CancellationToken ct = default)
    {
        _logger.LogInformation("FYCloseOrchestratorJob starting for FY job {FYJobId}", fyJobId);

        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct)
            ?? throw new InvalidOperationException($"FY Job {fyJobId} not found.");

        // Pre-validate
        var valid = await _fyCloseService.ValidatePreConditionsAsync(job.FYYear, ct);
        if (!valid)
        {
            _logger.LogError("FYCloseOrchestratorJob: Pre-conditions FAILED for {FYYear}", job.FYYear);
            return;
        }

        // Kick off Wave 1
        BackgroundJob.Enqueue<WaveDispatchJob>(j => j.ExecuteWave(fyJobId, 1, job.WaveSize, CancellationToken.None));
        _logger.LogInformation("FYCloseOrchestratorJob: Wave 1 enqueued for {FYJobId}", fyJobId);
    }
}

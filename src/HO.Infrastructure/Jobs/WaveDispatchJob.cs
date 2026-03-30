using HO.Application.Interfaces;
using HO.Application.Services;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Jobs;

public class WaveDispatchJob
{
    private readonly IFYCloseService _fyCloseService;
    private readonly IFYJobRepository _fyJobRepo;
    private readonly IStoreRepository _storeRepo;
    private readonly ILogger<WaveDispatchJob> _logger;

    public WaveDispatchJob(
        IFYCloseService fyCloseService,
        IFYJobRepository fyJobRepo,
        IStoreRepository storeRepo,
        ILogger<WaveDispatchJob> logger)
    {
        _fyCloseService = fyCloseService;
        _fyJobRepo = fyJobRepo;
        _storeRepo = storeRepo;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ExecuteWave(Guid fyJobId, int waveNumber, int waveSize, CancellationToken ct)
    {
        _logger.LogInformation("WaveDispatchJob: Dispatching Wave {Wave} for FY job {FYJobId}", waveNumber, fyJobId);

        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null || job.Status == HO.Domain.Enums.FYJobStatus.Cancelled ||
            job.Status == HO.Domain.Enums.FYJobStatus.Paused)
        {
            _logger.LogWarning("WaveDispatchJob: Job {FYJobId} is {Status} — stopping wave dispatch", fyJobId, job?.Status);
            return;
        }

        var pendingStores = (await _storeRepo.GetPendingFYCloseAsync(fyJobId, waveSize, ct)).ToList();

        if (!pendingStores.Any())
        {
            _logger.LogInformation("WaveDispatchJob: No more pending stores for {FYJobId} — closing job", fyJobId);
            await _fyCloseService.CloseJobAsync(fyJobId, ct);
            return;
        }

        // Dispatch commands for this wave
        await _fyCloseService.DispatchWaveAsync(fyJobId, waveNumber, ct);

        _logger.LogInformation("WaveDispatchJob: Wave {Wave} dispatched {Count} stores", waveNumber, pendingStores.Count);

        // Schedule next wave after interval
        BackgroundJob.Schedule<WaveDispatchJob>(
            j => j.ExecuteWave(fyJobId, waveNumber + 1, waveSize, CancellationToken.None),
            TimeSpan.FromMinutes(job.WaveIntervalMinutes));
    }
}

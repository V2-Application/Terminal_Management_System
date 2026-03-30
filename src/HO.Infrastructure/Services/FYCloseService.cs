using Hangfire;
using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.DomainEvents;
using HO.Domain.Entities;
using HO.Domain.Enums;
using HO.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Services;

public class FYCloseService : IFYCloseService
{
    private readonly IFYJobRepository    _fyJobRepo;
    private readonly IStoreRepository    _storeRepo;
    private readonly ITerminalRepository _terminalRepo;
    private readonly ICommandRepository  _commandRepo;
    private readonly IPackageRepository  _packageRepo;
    private readonly ISignalRService     _signalR;
    private readonly IAuditService       _audit;
    private readonly ILogger<FYCloseService> _logger;

    public FYCloseService(
        IFYJobRepository fyJobRepo, IStoreRepository storeRepo,
        ITerminalRepository terminalRepo, ICommandRepository commandRepo,
        IPackageRepository packageRepo, ISignalRService signalR,
        IAuditService audit, ILogger<FYCloseService> logger)
    {
        _fyJobRepo = fyJobRepo; _storeRepo = storeRepo;
        _terminalRepo = terminalRepo; _commandRepo = commandRepo;
        _packageRepo = packageRepo; _signalR = signalR;
        _audit = audit; _logger = logger;
    }

    public async Task<FinancialYearJob> StartJobAsync(
        string fyYear, Guid scriptPackageId, Guid rollbackPackageId,
        int waveSize, DateTime windowStart, DateTime windowEnd,
        string startedBy, CancellationToken ct = default)
    {
        // Guard: only one active FY job
        var existing = await _fyJobRepo.GetActiveJobAsync(ct);
        if (existing != null)
            throw new InvalidOperationException(
                "A FY-Close job for " + existing.FYYear + " is already active.");

        var pkg = await _packageRepo.GetByIdAsync(scriptPackageId, ct)
            ?? throw new KeyNotFoundException("Script package not found: " + scriptPackageId);

        var allStores = (await _storeRepo.GetAllAsync(StoreStatus.Active, ct)).ToList();

        // Reset all stores to Pending for this FY
        foreach (var s in allStores)
        {
            s.FYCloseStatus = FYCloseStatus.Pending;
            await _storeRepo.UpdateAsync(s, ct);
        }

        var job = new FinancialYearJob
        {
            FYYear               = fyYear,
            Status               = FYJobStatus.Running,
            Phase                = FYJobPhase.PreValidation,
            WaveSize             = waveSize,
            WaveIntervalMinutes  = 5,
            TotalStores          = allStores.Count,
            ScriptPackageId      = scriptPackageId,
            RollbackPackageId    = rollbackPackageId,
            ExecutionWindowStart = windowStart,
            ExecutionWindowEnd   = windowEnd,
            StartedAt            = DateTime.UtcNow,
            StartedBy            = startedBy
        };

        await _fyJobRepo.AddAsync(job, ct);

        // Kick off orchestration via Hangfire
        BackgroundJob.Enqueue<FYCloseOrchestratorJob>(
            j => j.StartOrchestration(job.FYJobId, CancellationToken.None));

        await _audit.LogAsync(startedBy, "FY_JOB_STARTED", "FinancialYearJob",
            job.FYJobId.ToString(), details: "FY=" + fyYear + " Stores=" + allStores.Count);

        _logger.LogInformation("FY-Close job started: {JobId} for {FYYear} ({Count} stores)",
            job.FYJobId, fyYear, allStores.Count);

        return job;
    }

    public async Task PauseJobAsync(Guid fyJobId, CancellationToken ct = default)
    {
        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null) return;
        job.Status = FYJobStatus.Paused;
        await _fyJobRepo.UpdateAsync(job, ct);
        _logger.LogInformation("FY job {Id} PAUSED", fyJobId);
    }

    public async Task ResumeJobAsync(Guid fyJobId, CancellationToken ct = default)
    {
        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null) return;
        job.Status = FYJobStatus.Running;
        await _fyJobRepo.UpdateAsync(job, ct);

        // Re-enqueue next wave
        BackgroundJob.Enqueue<WaveDispatchJob>(
            j => j.ExecuteWave(fyJobId, 1, job.WaveSize, CancellationToken.None));
        _logger.LogInformation("FY job {Id} RESUMED", fyJobId);
    }

    public async Task<bool> ValidatePreConditionsAsync(
        string fyYear, CancellationToken ct = default)
    {
        var activeTerminals = (await _terminalRepo.GetActiveTerminalsAsync(ct)).ToList();
        var totalStores     = (await _storeRepo.GetAllAsync(StoreStatus.Active, ct)).Count();
        var offlinePct      = totalStores > 0
            ? (double)(totalStores - activeTerminals.Count) / totalStores * 100
            : 100;

        if (offlinePct > 20)
        {
            _logger.LogWarning(
                "Pre-condition FAILED: {Pct:F1}% of stores offline (threshold 20%)", offlinePct);
            return false;
        }

        _logger.LogInformation(
            "Pre-conditions passed: {Active}/{Total} terminals online",
            activeTerminals.Count, totalStores);
        return true;
    }

    public async Task DispatchWaveAsync(
        Guid fyJobId, int waveNumber, CancellationToken ct = default)
    {
        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null) return;

        job.Phase = FYJobPhase.WaveDispatch;
        await _fyJobRepo.UpdateAsync(job, ct);

        var pendingStores =
            (await _storeRepo.GetPendingFYCloseAsync(fyJobId, job.WaveSize, ct)).ToList();

        _logger.LogInformation("Wave {N}: dispatching {Count} stores", waveNumber, pendingStores.Count);

        foreach (var store in pendingStores)
        {
            var primaryTerminal =
                (await _terminalRepo.GetByStoreAsync(store.StoreId, ct))
                .FirstOrDefault(t => t.IsPrimary);

            if (primaryTerminal == null)
            {
                _logger.LogWarning("Store {Code} has no primary terminal — skipping", store.StoreCode);
                store.FYCloseStatus = FYCloseStatus.Skipped;
                await _storeRepo.UpdateAsync(store, ct);
                continue;
            }

            var cmd = new Command
            {
                TerminalId  = primaryTerminal.TerminalId,
                StoreId     = store.StoreId,
                FYJobId     = fyJobId,
                CommandType = CommandType.FyClose,
                PackageId   = job.ScriptPackageId,
                Priority    = store.Priority,
                CreatedBy   = "SYSTEM",
                Status      = CommandStatus.Queued
            };
            await _commandRepo.AddAsync(cmd, ct);
        }

        await UpdateProgressAsync(fyJobId, ct);
    }

    public async Task UpdateProgressAsync(Guid fyJobId, CancellationToken ct = default)
    {
        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null) return;

        var allStores = (await _storeRepo.GetAllAsync(StoreStatus.Active, ct)).ToList();
        job.CompletedStores = allStores.Count(s => s.FYCloseStatus == FYCloseStatus.Completed);
        job.FailedStores    = allStores.Count(s => s.FYCloseStatus == FYCloseStatus.Failed);
        job.OfflineStores   = allStores.Count(s => s.FYCloseStatus == FYCloseStatus.Offline);
        await _fyJobRepo.UpdateAsync(job, ct);

        await _signalR.BroadcastFYJobProgress(new FYJobProgressEvent(
            fyJobId, job.CompletedStores, job.FailedStores,
            allStores.Count(s => s.FYCloseStatus == FYCloseStatus.Pending),
            job.OfflineStores, allStores.Count));
    }

    public async Task CloseJobAsync(Guid fyJobId, CancellationToken ct = default)
    {
        var job = await _fyJobRepo.GetByIdAsync(fyJobId, ct);
        if (job == null) return;

        job.Status      = FYJobStatus.Completed;
        job.Phase       = FYJobPhase.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _fyJobRepo.UpdateAsync(job, ct);

        await _audit.LogAsync("SYSTEM", "FY_JOB_COMPLETED", "FinancialYearJob",
            fyJobId.ToString(), details: "Completed=" + job.CompletedStores + " Failed=" + job.FailedStores);

        _logger.LogInformation(
            "FY-Close job {Id} COMPLETED. Completed={C} Failed={F} Offline={O}",
            fyJobId, job.CompletedStores, job.FailedStores, job.OfflineStores);
    }
}

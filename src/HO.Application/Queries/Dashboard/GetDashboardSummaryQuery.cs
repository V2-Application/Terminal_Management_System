using HO.Application.DTOs;
using HO.Application.Interfaces;
using HO.Domain.Enums;
using MediatR;

namespace HO.Application.Queries.Dashboard;

public record GetDashboardSummaryQuery(Guid? FYJobId) : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IFYJobRepository    _fyJobRepo;
    private readonly IStoreRepository    _storeRepo;
    private readonly ITerminalRepository _terminalRepo;
    private readonly ICommandRepository  _commandRepo;

    public GetDashboardSummaryQueryHandler(
        IFYJobRepository fyJobRepo,
        IStoreRepository storeRepo,
        ITerminalRepository terminalRepo,
        ICommandRepository commandRepo)
    {
        _fyJobRepo    = fyJobRepo;
        _storeRepo    = storeRepo;
        _terminalRepo = terminalRepo;
        _commandRepo  = commandRepo;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var stores    = (await _storeRepo.GetAllAsync(ct: ct)).ToList();
        var activeJob = await _fyJobRepo.GetActiveJobAsync(ct);

        // Count stores with currently RUNNING commands (dispatched/running)
        var runningCommands = await _commandRepo.GetRecentAsync(1000, ct);
        var runningStoreIds = runningCommands
            .Where(c => c.Status == CommandStatus.Running || c.Status == CommandStatus.Dispatched)
            .Select(c => c.StoreId)
            .ToHashSet();

        // Count offline = stores where primary terminal is offline
        var offlineTerminals = await _terminalRepo.GetOfflineTerminalsAsync(
            TimeSpan.FromMinutes(10), ct);
        var offlineStoreIds = offlineTerminals.Select(t => t.StoreId).ToHashSet();

        return new DashboardSummaryDto
        {
            TotalStores  = stores.Count,
            Completed    = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Completed),
            Running      = runningStoreIds.Count,
            Failed       = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Failed),
            Offline      = offlineStoreIds.Count,
            Pending      = stores.Count(s =>
                              s.FYCloseStatus == FYCloseStatus.Pending &&
                              !runningStoreIds.Contains(s.StoreId) &&
                              !offlineStoreIds.Contains(s.StoreId)),
            ActiveFYJobId = activeJob?.FYJobId.ToString(),
            ActiveFYYear  = activeJob?.FYYear,
            ActiveWave    = activeJob?.WaveSize ?? 0
        };
    }
}

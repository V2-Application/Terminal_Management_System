using HO.Application.DTOs;
using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.Enums;
using MediatR;

namespace HO.Application.Queries.Dashboard;

public record GetDashboardSummaryQuery(Guid? FYJobId) : IRequest<DashboardSummaryDto>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IFYJobRepository _fyJobRepo;
    private readonly IStoreRepository _storeRepo;

    public GetDashboardSummaryQueryHandler(IFYJobRepository fyJobRepo, IStoreRepository storeRepo)
    {
        _fyJobRepo = fyJobRepo;
        _storeRepo = storeRepo;
    }

    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken ct)
    {
        var stores = (await _storeRepo.GetAllAsync(ct: ct)).ToList();
        var activeJob = await _fyJobRepo.GetActiveJobAsync(ct);

        return new DashboardSummaryDto
        {
            TotalStores = stores.Count,
            Completed = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Completed),
            Failed = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Failed),
            Offline = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Offline),
            Pending = stores.Count(s => s.FYCloseStatus == FYCloseStatus.Pending),
            ActiveFYJobId = activeJob?.FYJobId.ToString(),
            ActiveFYYear = activeJob?.FYYear
        };
    }
}

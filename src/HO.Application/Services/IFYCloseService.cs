using HO.Domain.Entities;

namespace HO.Application.Services;

public interface IFYCloseService
{
    Task<FinancialYearJob> StartJobAsync(string fyYear, Guid scriptPackageId,
        Guid rollbackPackageId, int waveSize, DateTime windowStart, DateTime windowEnd,
        string startedBy, CancellationToken ct = default);
    Task PauseJobAsync(Guid fyJobId, CancellationToken ct = default);
    Task ResumeJobAsync(Guid fyJobId, CancellationToken ct = default);
    Task<bool> ValidatePreConditionsAsync(string fyYear, CancellationToken ct = default);
    Task DispatchWaveAsync(Guid fyJobId, int waveNumber, CancellationToken ct = default);
    Task UpdateProgressAsync(Guid fyJobId, CancellationToken ct = default);
    Task CloseJobAsync(Guid fyJobId, CancellationToken ct = default);
}

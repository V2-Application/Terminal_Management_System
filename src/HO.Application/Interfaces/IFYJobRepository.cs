using HO.Domain.Entities;

namespace HO.Application.Interfaces;

public interface IFYJobRepository
{
    Task<FinancialYearJob?> GetByIdAsync(Guid fyJobId, CancellationToken ct = default);
    Task<FinancialYearJob?> GetActiveJobAsync(CancellationToken ct = default);
    Task<FinancialYearJob?> GetByYearAsync(string fyYear, CancellationToken ct = default);
    Task AddAsync(FinancialYearJob job, CancellationToken ct = default);
    Task UpdateAsync(FinancialYearJob job, CancellationToken ct = default);
}

using HO.Application.Interfaces;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence.Repositories;

public class FYJobRepository : IFYJobRepository
{
    private readonly AppDbContext _db;
    public FYJobRepository(AppDbContext db) => _db = db;

    public async Task<FinancialYearJob?> GetByIdAsync(Guid fyJobId, CancellationToken ct = default)
        => await _db.FinancialYearJobs.FirstOrDefaultAsync(j => j.FYJobId == fyJobId, ct);

    public async Task<FinancialYearJob?> GetActiveJobAsync(CancellationToken ct = default)
        => await _db.FinancialYearJobs
            .FirstOrDefaultAsync(j =>
                j.Status == FYJobStatus.Running || j.Status == FYJobStatus.Paused, ct);

    public async Task<FinancialYearJob?> GetByYearAsync(string fyYear, CancellationToken ct = default)
        => await _db.FinancialYearJobs.FirstOrDefaultAsync(j => j.FYYear == fyYear, ct);

    public async Task<IEnumerable<FinancialYearJob>> GetAllAsync(CancellationToken ct = default)
        => await _db.FinancialYearJobs.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(FinancialYearJob job, CancellationToken ct = default)
    {
        await _db.FinancialYearJobs.AddAsync(job, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FinancialYearJob job, CancellationToken ct = default)
    {
        _db.FinancialYearJobs.Update(job);
        await _db.SaveChangesAsync(ct);
    }
}

using HO.Application.Interfaces;
using HO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence.Repositories;

public class PackageRepository : IPackageRepository
{
    private readonly AppDbContext _db;
    public PackageRepository(AppDbContext db) => _db = db;

    public async Task<ScriptPackage?> GetByIdAsync(Guid packageId, CancellationToken ct = default)
        => await _db.ScriptPackages.FirstOrDefaultAsync(p => p.PackageId == packageId && !p.IsDeleted, ct);

    public async Task<ScriptPackage?> GetActivePackageAsync(
        string stepType, string fyYear, CancellationToken ct = default)
        => await _db.ScriptPackages
            .FirstOrDefaultAsync(p =>
                p.StepType == stepType &&
                p.FYYear == fyYear &&
                p.IsActive &&
                !p.IsDeleted, ct);

    public async Task<IEnumerable<ScriptPackage>> GetAllAsync(CancellationToken ct = default)
        => await _db.ScriptPackages
            .Where(p => !p.IsDeleted)
            .OrderByDescending(p => p.UploadedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ScriptPackage package, CancellationToken ct = default)
    {
        await _db.ScriptPackages.AddAsync(package, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ScriptPackage package, CancellationToken ct = default)
    {
        _db.ScriptPackages.Update(package);
        await _db.SaveChangesAsync(ct);
    }
}

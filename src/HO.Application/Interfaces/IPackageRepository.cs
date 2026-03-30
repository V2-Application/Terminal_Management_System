using HO.Domain.Entities;

namespace HO.Application.Interfaces;

public interface IPackageRepository
{
    Task<ScriptPackage?> GetByIdAsync(Guid packageId, CancellationToken ct = default);
    Task<ScriptPackage?> GetActivePackageAsync(string stepType, string fyYear, CancellationToken ct = default);
    Task<IEnumerable<ScriptPackage>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(ScriptPackage package, CancellationToken ct = default);
    Task UpdateAsync(ScriptPackage package, CancellationToken ct = default);
}

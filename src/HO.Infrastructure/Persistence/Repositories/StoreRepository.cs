using HO.Application.Interfaces;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly AppDbContext _db;
    public StoreRepository(AppDbContext db) => _db = db;

    public async Task<Store?> GetByIdAsync(Guid storeId, CancellationToken ct = default)
        => await _db.Stores
            .Include(s => s.Terminals)
            .FirstOrDefaultAsync(s => s.StoreId == storeId, ct);

    public async Task<Store?> GetByCodeAsync(string storeCode, CancellationToken ct = default)
        => await _db.Stores
            .Include(s => s.Terminals)
            .FirstOrDefaultAsync(s => s.StoreCode == storeCode, ct);

    public async Task<IEnumerable<Store>> GetAllAsync(
        StoreStatus? status = null, CancellationToken ct = default)
    {
        var q = _db.Stores.Include(s => s.Terminals).AsQueryable();
        if (status.HasValue) q = q.Where(s => s.Status == status.Value);
        return await q.OrderBy(s => s.Region).ThenBy(s => s.StoreName).ToListAsync(ct);
    }

    public async Task<IEnumerable<Store>> GetByRegionAsync(
        string region, CancellationToken ct = default)
        => await _db.Stores
            .Include(s => s.Terminals)
            .Where(s => s.Region == region)
            .OrderBy(s => s.StoreName)
            .ToListAsync(ct);

    public async Task<IEnumerable<Store>> GetPendingFYCloseAsync(
        Guid fyJobId, int take, CancellationToken ct = default)
        => await _db.Stores
            .Include(s => s.Terminals)
            .Where(s => s.FYCloseStatus == FYCloseStatus.Pending && s.Status == StoreStatus.Active)
            .OrderBy(s => s.Priority).ThenBy(s => s.Region)
            .Take(take).ToListAsync(ct);

    public async Task AddAsync(Store store, CancellationToken ct = default)
    {
        await _db.Stores.AddAsync(store, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Store store, CancellationToken ct = default)
    {
        store.UpdatedAt = DateTime.UtcNow;
        _db.Stores.Update(store);
        await _db.SaveChangesAsync(ct);
    }
}

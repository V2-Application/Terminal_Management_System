using HO.Domain.Entities;
using HO.Domain.Enums;

namespace HO.Application.Interfaces;

public interface IStoreRepository
{
    Task<Store?> GetByIdAsync(Guid storeId, CancellationToken ct = default);
    Task<Store?> GetByCodeAsync(string storeCode, CancellationToken ct = default);
    Task<IEnumerable<Store>> GetAllAsync(StoreStatus? status = null, CancellationToken ct = default);
    Task<IEnumerable<Store>> GetByRegionAsync(string region, CancellationToken ct = default);
    Task<IEnumerable<Store>> GetPendingFYCloseAsync(Guid fyJobId, int take, CancellationToken ct = default);
    Task AddAsync(Store store, CancellationToken ct = default);
    Task UpdateAsync(Store store, CancellationToken ct = default);
}

// ensure the interface is accessible

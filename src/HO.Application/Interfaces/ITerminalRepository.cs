using HO.Domain.Entities;
using HO.Domain.Enums;

namespace HO.Application.Interfaces;

public interface ITerminalRepository
{
    Task<Terminal?> GetByIdAsync(Guid terminalId, CancellationToken ct = default);
    Task<Terminal?> GetByMachineIdAsync(string machineId, CancellationToken ct = default);
    Task<IEnumerable<Terminal>> GetByStoreAsync(Guid storeId, CancellationToken ct = default);
    Task<IEnumerable<Terminal>> GetOfflineTerminalsAsync(TimeSpan threshold, CancellationToken ct = default);
    Task<IEnumerable<Terminal>> GetActiveTerminalsAsync(CancellationToken ct = default);
    Task AddAsync(Terminal terminal, CancellationToken ct = default);
    Task UpdateAsync(Terminal terminal, CancellationToken ct = default);
}

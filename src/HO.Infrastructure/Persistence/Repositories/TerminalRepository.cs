using HO.Application.Interfaces;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence.Repositories;

public class TerminalRepository : ITerminalRepository
{
    private readonly AppDbContext _db;
    public TerminalRepository(AppDbContext db) => _db = db;

    public async Task<Terminal?> GetByIdAsync(Guid terminalId, CancellationToken ct = default)
        => await _db.Terminals.Include(t => t.Store).FirstOrDefaultAsync(t => t.TerminalId == terminalId, ct);

    public async Task<Terminal?> GetByMachineIdAsync(string machineId, CancellationToken ct = default)
        => await _db.Terminals.FirstOrDefaultAsync(t => t.MachineId == machineId, ct);

    public async Task<IEnumerable<Terminal>> GetByStoreAsync(Guid storeId, CancellationToken ct = default)
        => await _db.Terminals.Where(t => t.StoreId == storeId).ToListAsync(ct);

    public async Task<IEnumerable<Terminal>> GetOfflineTerminalsAsync(TimeSpan threshold, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - threshold;
        return await _db.Terminals
            .Include(t => t.Store)
            .Where(t => t.Status == TerminalStatus.Active && t.LastHeartbeat < cutoff)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<Terminal>> GetActiveTerminalsAsync(CancellationToken ct = default)
        => await _db.Terminals.Where(t => t.Status == TerminalStatus.Active).ToListAsync(ct);

    public async Task AddAsync(Terminal terminal, CancellationToken ct = default)
    {
        await _db.Terminals.AddAsync(terminal, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Terminal terminal, CancellationToken ct = default)
    {
        terminal.UpdatedAt = DateTime.UtcNow;
        _db.Terminals.Update(terminal);
        await _db.SaveChangesAsync(ct);
    }
}

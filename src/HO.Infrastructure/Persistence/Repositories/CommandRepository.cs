using HO.Application.Interfaces;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HO.Infrastructure.Persistence.Repositories;

public class CommandRepository : ICommandRepository
{
    private readonly AppDbContext _db;
    public CommandRepository(AppDbContext db) => _db = db;

    public async Task<Command?> GetByIdAsync(Guid commandId, CancellationToken ct = default)
        => await _db.Commands
            .Include(c => c.Executions)
            .Include(c => c.Terminal).ThenInclude(t => t.Store)
            .FirstOrDefaultAsync(c => c.CommandId == commandId, ct);

    public async Task<IEnumerable<Command>> GetPendingForTerminalAsync(
        Guid terminalId, CancellationToken ct = default)
        => await _db.Commands
            .Where(c => c.TerminalId == terminalId
                && c.Status == CommandStatus.Queued
                && c.ScheduledFor <= DateTime.UtcNow
                && c.ScheduledFor.AddMinutes(c.TTLMinutes) > DateTime.UtcNow)
            .OrderBy(c => c.Priority).ThenBy(c => c.ScheduledFor)
            .Take(5)
            .ToListAsync(ct);

    public async Task<IEnumerable<Command>> GetFailedForRetryAsync(
        DateTime retryBefore, CancellationToken ct = default)
        => await _db.Commands
            .Where(c => c.Status == CommandStatus.Failed
                && c.RetryCount < c.MaxRetries
                && c.RetryAfter.HasValue
                && c.RetryAfter <= retryBefore)
            .ToListAsync(ct);

    public async Task<IEnumerable<Command>> GetByFYJobAsync(
        Guid fyJobId, CancellationToken ct = default)
        => await _db.Commands
            .Where(c => c.FYJobId == fyJobId)
            .Include(c => c.Executions)
            .ToListAsync(ct);

    public async Task<IEnumerable<Command>> GetRecentAsync(
        int count = 100, CancellationToken ct = default)
        => await _db.Commands
            .Include(c => c.Terminal).ThenInclude(t => t.Store)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task AddAsync(Command command, CancellationToken ct = default)
    {
        await _db.Commands.AddAsync(command, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Command command, CancellationToken ct = default)
    {
        _db.Commands.Update(command);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddExecutionAsync(CommandExecution execution, CancellationToken ct = default)
    {
        await _db.CommandExecutions.AddAsync(execution, ct);
        await _db.SaveChangesAsync(ct);
    }
}

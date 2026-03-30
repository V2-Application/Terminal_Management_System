using HO.Domain.Entities;
using HO.Domain.Enums;

namespace HO.Application.Interfaces;

public interface ICommandRepository
{
    Task<Command?> GetByIdAsync(Guid commandId, CancellationToken ct = default);
    Task<IEnumerable<Command>> GetPendingForTerminalAsync(Guid terminalId, CancellationToken ct = default);
    Task<IEnumerable<Command>> GetFailedForRetryAsync(DateTime retryBefore, CancellationToken ct = default);
    Task<IEnumerable<Command>> GetByFYJobAsync(Guid fyJobId, CancellationToken ct = default);
    Task AddAsync(Command command, CancellationToken ct = default);
    Task UpdateAsync(Command command, CancellationToken ct = default);
    Task AddExecutionAsync(CommandExecution execution, CancellationToken ct = default);
}

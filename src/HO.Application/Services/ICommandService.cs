using HO.Domain.Entities;
using HO.Domain.Enums;

namespace HO.Application.Services;

public interface ICommandService
{
    Task<IEnumerable<Command>> GetPendingCommandsAsync(Guid terminalId, CancellationToken ct = default);
    Task AcknowledgeCommandAsync(Guid commandId, Guid terminalId, CancellationToken ct = default);
    Task RecordExecutionStartAsync(Guid commandId, Guid terminalId, CancellationToken ct = default);
    Task RecordExecutionResultAsync(Guid commandId, Guid terminalId, int exitCode,
        string? stdout, string? stderr, long durationMs, CancellationToken ct = default);
    Task<Command> CreateCommandAsync(Guid terminalId, Guid storeId, CommandType type,
        Guid? packageId, Guid? fyJobId, int priority, string createdBy, CancellationToken ct = default);
    Task RetryCommandAsync(Guid commandId, CancellationToken ct = default);
    Task RollbackStoreAsync(Guid storeId, Guid fyJobId, string initiatedBy, CancellationToken ct = default);
    Task CancelCommandAsync(Guid commandId, string cancelledBy, CancellationToken ct = default);
}

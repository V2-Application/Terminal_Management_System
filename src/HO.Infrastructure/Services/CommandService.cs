using HO.Application.Interfaces;
using HO.Application.Services;
using HO.Domain.DomainEvents;
using HO.Domain.Entities;
using HO.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace HO.Infrastructure.Services;

public class CommandService : ICommandService
{
    private readonly ICommandRepository  _commands;
    private readonly ITerminalRepository _terminals;
    private readonly IStoreRepository    _stores;
    private readonly IFYJobRepository    _fyJobs;
    private readonly IAuditService       _audit;
    private readonly ISignalRService     _signalR;
    private readonly ILogger<CommandService> _logger;

    public CommandService(
        ICommandRepository commands, ITerminalRepository terminals,
        IStoreRepository stores, IFYJobRepository fyJobs,
        IAuditService audit, ISignalRService signalR,
        ILogger<CommandService> logger)
    {
        _commands = commands; _terminals = terminals;
        _stores = stores; _fyJobs = fyJobs;
        _audit = audit; _signalR = signalR; _logger = logger;
    }

    public async Task<IEnumerable<Command>> GetPendingCommandsAsync(
        Guid terminalId, CancellationToken ct = default)
        => await _commands.GetPendingForTerminalAsync(terminalId, ct);

    public async Task AcknowledgeCommandAsync(
        Guid commandId, Guid terminalId, CancellationToken ct = default)
    {
        var cmd = await _commands.GetByIdAsync(commandId, ct);
        if (cmd == null || cmd.Status == CommandStatus.Dispatched) return; // idempotent

        cmd.Status       = CommandStatus.Dispatched;
        cmd.DispatchedAt = DateTime.UtcNow;
        await _commands.UpdateAsync(cmd, ct);

        await _signalR.BroadcastCommandStatusUpdated(new CommandStatusUpdatedEvent(
            commandId, cmd.StoreId, terminalId, "Dispatched",
            cmd.CommandType.ToString(), null, DateTime.UtcNow));
    }

    public async Task RecordExecutionStartAsync(
        Guid commandId, Guid terminalId, CancellationToken ct = default)
    {
        var cmd = await _commands.GetByIdAsync(commandId, ct);
        if (cmd == null) return;

        cmd.Status    = CommandStatus.Running;
        cmd.StartedAt = DateTime.UtcNow;
        await _commands.UpdateAsync(cmd, ct);

        await _signalR.BroadcastCommandStatusUpdated(new CommandStatusUpdatedEvent(
            commandId, cmd.StoreId, terminalId, "Running",
            cmd.CommandType.ToString(), 10, DateTime.UtcNow));
    }

    public async Task RecordExecutionResultAsync(
        Guid commandId, Guid terminalId, int exitCode,
        string? stdout, string? stderr, long durationMs, CancellationToken ct = default)
    {
        var cmd = await _commands.GetByIdAsync(commandId, ct);
        if (cmd == null) return;

        // Save execution record
        await _commands.AddExecutionAsync(new CommandExecution
        {
            CommandId     = commandId,
            TerminalId    = terminalId,
            AttemptNumber = cmd.RetryCount + 1,
            ExitCode      = exitCode,
            Stdout        = stdout,
            Stderr        = stderr,
            DurationMs    = durationMs,
            StartedAt     = cmd.StartedAt ?? DateTime.UtcNow,
            CompletedAt   = DateTime.UtcNow
        }, ct);

        var succeeded  = exitCode == 0;
        cmd.Status     = succeeded ? CommandStatus.Success : CommandStatus.Failed;
        cmd.CompletedAt = DateTime.UtcNow;

        if (!succeeded && cmd.RetryCount < cmd.MaxRetries)
            cmd.RetryAfter = DateTime.UtcNow.AddMinutes(Math.Pow(5, cmd.RetryCount + 1));

        await _commands.UpdateAsync(cmd, ct);

        // Update store status
        if (cmd.FYJobId.HasValue)
        {
            var store = await _stores.GetByIdAsync(cmd.StoreId, ct);
            if (store != null)
            {
                store.FYCloseStatus = succeeded ? FYCloseStatus.Completed
                    : cmd.RetryCount >= cmd.MaxRetries ? FYCloseStatus.Failed
                    : store.FYCloseStatus;
                await _stores.UpdateAsync(store, ct);
            }

            if (succeeded)
            {
                var fyJob = await _fyJobs.GetByIdAsync(cmd.FYJobId.Value, ct);
                if (fyJob != null) { fyJob.CompletedStores++; await _fyJobs.UpdateAsync(fyJob, ct); }
            }
        }

        await _signalR.BroadcastCommandStatusUpdated(new CommandStatusUpdatedEvent(
            commandId, cmd.StoreId, terminalId,
            cmd.Status.ToString(), cmd.CommandType.ToString(), 100, DateTime.UtcNow));

        await _audit.LogAsync("AGENT:" + terminalId, "COMMAND_RESULT", "Command",
            commandId.ToString(), details: "ExitCode=" + exitCode + " Duration=" + durationMs + "ms");
    }

    public async Task<Command> CreateCommandAsync(
        Guid terminalId, Guid storeId, CommandType type,
        Guid? packageId, Guid? fyJobId, int priority,
        string createdBy, CancellationToken ct = default)
    {
        var cmd = new Command
        {
            TerminalId  = terminalId,
            StoreId     = storeId,
            FYJobId     = fyJobId,
            CommandType = type,
            PackageId   = packageId,
            Priority    = priority,
            CreatedBy   = createdBy,
            Status      = CommandStatus.Queued
        };
        await _commands.AddAsync(cmd, ct);
        return cmd;
    }

    public async Task RetryCommandAsync(Guid commandId, CancellationToken ct = default)
    {
        var cmd = await _commands.GetByIdAsync(commandId, ct);
        if (cmd == null) return;

        cmd.RetryCount++;
        cmd.Status       = CommandStatus.Queued;
        cmd.RetryAfter   = null;
        cmd.ScheduledFor = DateTime.UtcNow;
        await _commands.UpdateAsync(cmd, ct);
        _logger.LogInformation("Command {Id} queued for retry #{N}", commandId, cmd.RetryCount);
    }

    public async Task RollbackStoreAsync(
        Guid storeId, Guid fyJobId, string initiatedBy, CancellationToken ct = default)
    {
        var fyJob = await _fyJobs.GetByIdAsync(fyJobId, ct);
        if (fyJob?.RollbackPackageId == null)
        {
            _logger.LogWarning("No rollback package configured for FY job {Id}", fyJobId);
            return;
        }

        var primaryTerminal =
            (await _terminals.GetByStoreAsync(storeId, ct)).FirstOrDefault(t => t.IsPrimary);
        if (primaryTerminal == null) return;

        var rollback = new Command
        {
            TerminalId  = primaryTerminal.TerminalId,
            StoreId     = storeId,
            FYJobId     = fyJobId,
            CommandType = CommandType.Rollback,
            PackageId   = fyJob.RollbackPackageId,
            Priority    = 1,
            CreatedBy   = initiatedBy,
            Status      = CommandStatus.Queued
        };
        await _commands.AddAsync(rollback, ct);

        await _audit.LogAsync(initiatedBy, "ROLLBACK_INITIATED", "Store",
            storeId.ToString(), details: "FYJob=" + fyJobId);

        _logger.LogInformation("Rollback command created for store {StoreId}", storeId);
    }

    public async Task CancelCommandAsync(
        Guid commandId, string cancelledBy, CancellationToken ct = default)
    {
        var cmd = await _commands.GetByIdAsync(commandId, ct);
        if (cmd?.Status != CommandStatus.Queued) return;

        cmd.Status = CommandStatus.Cancelled;
        await _commands.UpdateAsync(cmd, ct);
        await _audit.LogAsync(cancelledBy, "COMMAND_CANCELLED", "Command", commandId.ToString());
    }
}

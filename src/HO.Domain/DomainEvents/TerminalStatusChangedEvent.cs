namespace HO.Domain.DomainEvents;

public record TerminalStatusChangedEvent(
    Guid TerminalId,
    Guid StoreId,
    string OldStatus,
    string NewStatus,
    DateTime OccurredAt);

public record CommandStatusUpdatedEvent(
    Guid CommandId,
    Guid StoreId,
    Guid TerminalId,
    string Status,
    string? StepName,
    int? Progress,
    DateTime OccurredAt);

public record FYJobProgressEvent(
    Guid FYJobId,
    int CompletedCount,
    int FailedCount,
    int PendingCount,
    int OfflineCount,
    int TotalCount);

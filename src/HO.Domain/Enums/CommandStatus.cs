namespace HO.Domain.Enums;

public enum CommandStatus
{
    Queued,
    Dispatched,
    Running,
    Success,
    Failed,
    Retrying,
    RolledBack,
    Cancelled,
    Expired
}

public enum CommandType
{
    NsoSetup,
    FyClose,
    TimeSyncTest,
    TimeSyncProd,
    Rollback,
    AgentUpdate,
    BillingLock,
    BillingUnlock
}

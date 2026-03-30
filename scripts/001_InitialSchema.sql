-- RetailTMS Initial Database Schema
-- SQL Server 2022
-- Run once to create all tables, indexes, and constraints

USE RetailTMS;
GO

-- ============================================================
-- STORES
-- ============================================================
CREATE TABLE dbo.Stores (
    StoreId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Stores PRIMARY KEY,
    StoreCode       VARCHAR(20)      NOT NULL,
    StoreName       NVARCHAR(100)    NOT NULL,
    Region          VARCHAR(50)      NOT NULL,
    Zone            VARCHAR(50)      NOT NULL,
    Address         NVARCHAR(500)    NULL,
    ContactEmail    VARCHAR(200)     NULL,
    ContactPhone    VARCHAR(20)      NULL,
    Priority        TINYINT          NOT NULL DEFAULT 2,
    Status          VARCHAR(20)      NOT NULL DEFAULT 'Active',
    FYCloseStatus   VARCHAR(20)      NOT NULL DEFAULT 'Pending',
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       NVARCHAR(100)    NOT NULL DEFAULT 'SYSTEM'
);
GO
CREATE UNIQUE INDEX UX_Stores_StoreCode ON dbo.Stores(StoreCode) WHERE IsDeleted = 0;
CREATE INDEX IX_Stores_Region ON dbo.Stores(Region);
CREATE INDEX IX_Stores_FYCloseStatus ON dbo.Stores(FYCloseStatus);
GO

-- ============================================================
-- TERMINALS
-- ============================================================
CREATE TABLE dbo.Terminals (
    TerminalId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Terminals PRIMARY KEY,
    StoreId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Terminals_Stores REFERENCES dbo.Stores(StoreId),
    TerminalCode    VARCHAR(30)      NOT NULL,
    MachineId       VARCHAR(100)     NOT NULL,
    MachineName     NVARCHAR(100)    NOT NULL,
    IpAddress       VARCHAR(50)      NULL,
    OsVersion       VARCHAR(100)     NULL,
    AgentVersion    VARCHAR(20)      NULL,
    PosVersion      VARCHAR(50)      NULL,
    Status          VARCHAR(20)      NOT NULL DEFAULT 'Unregistered',
    IsPrimary       BIT              NOT NULL DEFAULT 0,
    LastHeartbeat   DATETIME2        NULL,
    DiskFreeGB      DECIMAL(10,2)    NULL,
    AuthTokenHash   VARCHAR(200)     NULL,
    TokenExpiry     DATETIME2        NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE UNIQUE INDEX UX_Terminals_TerminalCode ON dbo.Terminals(TerminalCode) WHERE IsDeleted = 0;
CREATE UNIQUE INDEX UX_Terminals_MachineId ON dbo.Terminals(MachineId) WHERE IsDeleted = 0;
CREATE INDEX IX_Terminals_StoreId ON dbo.Terminals(StoreId);
CREATE INDEX IX_Terminals_LastHeartbeat ON dbo.Terminals(LastHeartbeat);
CREATE INDEX IX_Terminals_Status ON dbo.Terminals(Status);
GO

-- ============================================================
-- SCRIPT PACKAGES
-- ============================================================
CREATE TABLE dbo.ScriptPackages (
    PackageId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_ScriptPackages PRIMARY KEY,
    PackageName         NVARCHAR(100)    NOT NULL,
    StepType            VARCHAR(50)      NOT NULL,
    Version             VARCHAR(20)      NOT NULL,
    DllVersion          VARCHAR(50)      NULL,
    FileSize            BIGINT           NOT NULL,
    Sha256Hash          VARCHAR(64)      NOT NULL,
    RsaSignature        NVARCHAR(MAX)    NULL,
    StoragePath         VARCHAR(500)     NOT NULL,
    IsActive            BIT              NOT NULL DEFAULT 0,
    IsRollbackPackage   BIT              NOT NULL DEFAULT 0,
    FYYear              VARCHAR(10)      NULL,
    UploadedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UploadedBy          NVARCHAR(100)    NOT NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);
GO

-- ============================================================
-- FINANCIAL YEAR JOBS
-- ============================================================
CREATE TABLE dbo.FinancialYearJobs (
    FYJobId                 UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_FYJobs PRIMARY KEY,
    FYYear                  VARCHAR(10)      NOT NULL,
    Status                  VARCHAR(20)      NOT NULL DEFAULT 'Draft',
    Phase                   VARCHAR(30)      NOT NULL DEFAULT 'PreValidation',
    WaveSize                INT              NOT NULL DEFAULT 50,
    WaveIntervalMinutes     INT              NOT NULL DEFAULT 5,
    TotalStores             INT              NULL,
    CompletedStores         INT              NOT NULL DEFAULT 0,
    FailedStores            INT              NOT NULL DEFAULT 0,
    OfflineStores           INT              NOT NULL DEFAULT 0,
    ScriptPackageId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_FYJobs_Package REFERENCES dbo.ScriptPackages(PackageId),
    RollbackPackageId       UNIQUEIDENTIFIER NULL     CONSTRAINT FK_FYJobs_Rollback REFERENCES dbo.ScriptPackages(PackageId),
    ExecutionWindowStart    DATETIME2        NOT NULL,
    ExecutionWindowEnd      DATETIME2        NOT NULL,
    StartedAt               DATETIME2        NULL,
    CompletedAt             DATETIME2        NULL,
    StartedBy               NVARCHAR(100)    NOT NULL,
    SummaryReportPath       VARCHAR(500)     NULL,
    CreatedAt               DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);
GO
CREATE UNIQUE INDEX UX_FYJobs_Year ON dbo.FinancialYearJobs(FYYear);
GO

-- ============================================================
-- COMMANDS
-- ============================================================
CREATE TABLE dbo.Commands (
    CommandId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_Commands PRIMARY KEY,
    TerminalId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Commands_Terminal REFERENCES dbo.Terminals(TerminalId),
    StoreId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Commands_Store REFERENCES dbo.Stores(StoreId),
    FYJobId         UNIQUEIDENTIFIER NULL     CONSTRAINT FK_Commands_FYJob REFERENCES dbo.FinancialYearJobs(FYJobId),
    CommandType     VARCHAR(50)      NOT NULL,
    CommandNonce    UNIQUEIDENTIFIER NOT NULL,
    PackageId       UNIQUEIDENTIFIER NULL     CONSTRAINT FK_Commands_Package REFERENCES dbo.ScriptPackages(PackageId),
    Status          VARCHAR(20)      NOT NULL DEFAULT 'Queued',
    Priority        TINYINT          NOT NULL DEFAULT 5,
    TTLMinutes      INT              NOT NULL DEFAULT 240,
    ScheduledFor    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    DispatchedAt    DATETIME2        NULL,
    StartedAt       DATETIME2        NULL,
    CompletedAt     DATETIME2        NULL,
    RetryCount      TINYINT          NOT NULL DEFAULT 0,
    MaxRetries      TINYINT          NOT NULL DEFAULT 3,
    RetryAfter      DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CreatedBy       NVARCHAR(100)    NOT NULL DEFAULT 'SYSTEM'
);
GO
CREATE UNIQUE INDEX UX_Commands_TerminalNonce ON dbo.Commands(TerminalId, CommandNonce);
CREATE INDEX IX_Commands_Status ON dbo.Commands(Status) WHERE Status IN ('Queued','Dispatched','Running');
CREATE INDEX IX_Commands_TerminalStatus ON dbo.Commands(TerminalId, Status);
CREATE INDEX IX_Commands_FYJobId ON dbo.Commands(FYJobId);
GO

-- ============================================================
-- COMMAND EXECUTIONS
-- ============================================================
CREATE TABLE dbo.CommandExecutions (
    ExecutionId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID() CONSTRAINT PK_CommandExecutions PRIMARY KEY,
    CommandId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Executions_Command REFERENCES dbo.Commands(CommandId),
    TerminalId      UNIQUEIDENTIFIER NOT NULL,
    AttemptNumber   TINYINT          NOT NULL DEFAULT 1,
    ExitCode        INT              NULL,
    Stdout          NVARCHAR(MAX)    NULL,
    Stderr          NVARCHAR(MAX)    NULL,
    DurationMs      BIGINT           NULL,
    AgentVersion    VARCHAR(20)      NULL,
    StartedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2        NULL
);
GO
CREATE INDEX IX_CommandExecutions_CommandId ON dbo.CommandExecutions(CommandId);
CREATE INDEX IX_CommandExecutions_TerminalId ON dbo.CommandExecutions(TerminalId, StartedAt DESC);
GO

-- ============================================================
-- HEARTBEATS
-- ============================================================
CREATE TABLE dbo.Heartbeats (
    HeartbeatId         BIGINT           NOT NULL IDENTITY(1,1) CONSTRAINT PK_Heartbeats PRIMARY KEY,
    TerminalId          UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Heartbeats_Terminal REFERENCES dbo.Terminals(TerminalId),
    ReceivedAt          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    AgentVersion        VARCHAR(20)      NULL,
    Status              VARCHAR(20)      NOT NULL DEFAULT 'ACTIVE',
    DiskFreeGB          DECIMAL(10,2)    NULL,
    PosProcessRunning   BIT              NOT NULL DEFAULT 0,
    LocalTime           DATETIME2        NULL
);
GO
CREATE INDEX IX_Heartbeats_TerminalId ON dbo.Heartbeats(TerminalId, ReceivedAt DESC);
GO

-- ============================================================
-- AUDIT LOGS (INSERT-ONLY — trigger blocks DELETE)
-- ============================================================
CREATE TABLE dbo.AuditLogs (
    AuditId     BIGINT        NOT NULL IDENTITY(1,1) CONSTRAINT PK_AuditLogs PRIMARY KEY,
    Timestamp   DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    UserId      NVARCHAR(100) NOT NULL,
    IpAddress   VARCHAR(50)   NULL,
    Action      VARCHAR(100)  NOT NULL,
    EntityType  VARCHAR(50)   NOT NULL,
    EntityId    VARCHAR(100)  NOT NULL,
    OldValue    NVARCHAR(MAX) NULL,
    NewValue    NVARCHAR(MAX) NULL,
    Result      VARCHAR(20)   NOT NULL DEFAULT 'SUCCESS',
    Details     NVARCHAR(MAX) NULL
);
GO
CREATE INDEX IX_AuditLogs_Timestamp ON dbo.AuditLogs(Timestamp DESC);
CREATE INDEX IX_AuditLogs_EntityType ON dbo.AuditLogs(EntityType, EntityId);
GO

-- Prevent deletions from AuditLogs
CREATE TRIGGER TR_AuditLogs_BlockDelete ON dbo.AuditLogs INSTEAD OF DELETE AS
BEGIN
    RAISERROR('Audit log records cannot be deleted.', 16, 1);
    ROLLBACK;
END;
GO

PRINT 'RetailTMS schema created successfully.';
GO

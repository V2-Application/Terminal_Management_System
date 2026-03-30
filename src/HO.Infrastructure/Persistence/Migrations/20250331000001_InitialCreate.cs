using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HO.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Stores",
                columns: table => new
                {
                    StoreId       = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    StoreCode     = table.Column<string>(maxLength: 20, nullable: false),
                    StoreName     = table.Column<string>(maxLength: 100, nullable: false),
                    Region        = table.Column<string>(maxLength: 50, nullable: false),
                    Zone          = table.Column<string>(maxLength: 50, nullable: false),
                    Address       = table.Column<string>(maxLength: 500, nullable: true),
                    ContactEmail  = table.Column<string>(maxLength: 200, nullable: true),
                    ContactPhone  = table.Column<string>(maxLength: 20, nullable: true),
                    Priority      = table.Column<byte>(nullable: false, defaultValue: (byte)2),
                    Status        = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Active"),
                    FYCloseStatus = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Pending"),
                    IsDeleted     = table.Column<bool>(nullable: false, defaultValue: false),
                    CreatedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy     = table.Column<string>(maxLength: 100, nullable: false, defaultValue: "SYSTEM"),
                },
                constraints: t => t.PrimaryKey("PK_Stores", x => x.StoreId));

            migrationBuilder.CreateIndex("UX_Stores_StoreCode", "Stores", "StoreCode", unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex("IX_Stores_Region",       "Stores", "Region");
            migrationBuilder.CreateIndex("IX_Stores_FYCloseStatus","Stores", "FYCloseStatus");

            migrationBuilder.CreateTable(
                name: "ScriptPackages",
                columns: table => new
                {
                    PackageId         = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    PackageName       = table.Column<string>(maxLength: 100, nullable: false),
                    StepType          = table.Column<string>(maxLength: 50, nullable: false),
                    Version           = table.Column<string>(maxLength: 20, nullable: false),
                    DllVersion        = table.Column<string>(maxLength: 50, nullable: true),
                    FileSize          = table.Column<long>(nullable: false),
                    Sha256Hash        = table.Column<string>(maxLength: 64, nullable: false),
                    RsaSignature      = table.Column<string>(nullable: true),
                    StoragePath       = table.Column<string>(maxLength: 500, nullable: false),
                    IsActive          = table.Column<bool>(nullable: false, defaultValue: false),
                    IsRollbackPackage = table.Column<bool>(nullable: false, defaultValue: false),
                    FYYear            = table.Column<string>(maxLength: 10, nullable: true),
                    UploadedAt        = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UploadedBy        = table.Column<string>(maxLength: 100, nullable: false),
                    IsDeleted         = table.Column<bool>(nullable: false, defaultValue: false),
                },
                constraints: t => t.PrimaryKey("PK_ScriptPackages", x => x.PackageId));

            migrationBuilder.CreateTable(
                name: "FinancialYearJobs",
                columns: table => new
                {
                    FYJobId              = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    FYYear               = table.Column<string>(maxLength: 10, nullable: false),
                    Status               = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Draft"),
                    Phase                = table.Column<string>(maxLength: 30, nullable: false, defaultValue: "PreValidation"),
                    WaveSize             = table.Column<int>(nullable: false, defaultValue: 50),
                    WaveIntervalMinutes  = table.Column<int>(nullable: false, defaultValue: 5),
                    TotalStores          = table.Column<int>(nullable: true),
                    CompletedStores      = table.Column<int>(nullable: false, defaultValue: 0),
                    FailedStores         = table.Column<int>(nullable: false, defaultValue: 0),
                    OfflineStores        = table.Column<int>(nullable: false, defaultValue: 0),
                    ScriptPackageId      = table.Column<Guid>(nullable: false),
                    RollbackPackageId    = table.Column<Guid>(nullable: true),
                    ExecutionWindowStart = table.Column<DateTime>(nullable: false),
                    ExecutionWindowEnd   = table.Column<DateTime>(nullable: false),
                    StartedAt            = table.Column<DateTime>(nullable: true),
                    CompletedAt          = table.Column<DateTime>(nullable: true),
                    StartedBy            = table.Column<string>(maxLength: 100, nullable: false),
                    SummaryReportPath    = table.Column<string>(maxLength: 500, nullable: true),
                    CreatedAt            = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                },
                constraints: t =>
                {
                    t.PrimaryKey("PK_FinancialYearJobs", x => x.FYJobId);
                    t.ForeignKey("FK_FYJobs_Package",  x => x.ScriptPackageId,   "ScriptPackages", "PackageId");
                    t.ForeignKey("FK_FYJobs_Rollback", x => x.RollbackPackageId, "ScriptPackages", "PackageId");
                });

            migrationBuilder.CreateIndex("UX_FYJobs_Year", "FinancialYearJobs", "FYYear", unique: true);

            migrationBuilder.CreateTable(
                name: "Terminals",
                columns: table => new
                {
                    TerminalId    = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    StoreId       = table.Column<Guid>(nullable: false),
                    TerminalCode  = table.Column<string>(maxLength: 30, nullable: false),
                    MachineId     = table.Column<string>(maxLength: 100, nullable: false),
                    MachineName   = table.Column<string>(maxLength: 100, nullable: false),
                    IpAddress     = table.Column<string>(maxLength: 50, nullable: true),
                    OsVersion     = table.Column<string>(maxLength: 100, nullable: true),
                    AgentVersion  = table.Column<string>(maxLength: 20, nullable: true),
                    PosVersion    = table.Column<string>(maxLength: 50, nullable: true),
                    Status        = table.Column<string>(maxLength: 30, nullable: false, defaultValue: "Unregistered"),
                    IsPrimary     = table.Column<bool>(nullable: false, defaultValue: false),
                    LastHeartbeat = table.Column<DateTime>(nullable: true),
                    DiskFreeGB    = table.Column<decimal>(precision: 10, scale: 2, nullable: true),
                    AuthTokenHash = table.Column<string>(maxLength: 200, nullable: true),
                    TokenExpiry   = table.Column<DateTime>(nullable: true),
                    IsDeleted     = table.Column<bool>(nullable: false, defaultValue: false),
                    CreatedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UpdatedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                },
                constraints: t =>
                {
                    t.PrimaryKey("PK_Terminals", x => x.TerminalId);
                    t.ForeignKey("FK_Terminals_Stores", x => x.StoreId, "Stores", "StoreId", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("UX_Terminals_TerminalCode", "Terminals", "TerminalCode", unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex("UX_Terminals_MachineId",   "Terminals", "MachineId",    unique: true, filter: "[IsDeleted] = 0");
            migrationBuilder.CreateIndex("IX_Terminals_StoreId",     "Terminals", "StoreId");
            migrationBuilder.CreateIndex("IX_Terminals_LastHB",      "Terminals", "LastHeartbeat");
            migrationBuilder.CreateIndex("IX_Terminals_Status",      "Terminals", "Status");

            migrationBuilder.CreateTable(
                name: "Commands",
                columns: table => new
                {
                    CommandId    = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    TerminalId   = table.Column<Guid>(nullable: false),
                    StoreId      = table.Column<Guid>(nullable: false),
                    FYJobId      = table.Column<Guid>(nullable: true),
                    CommandType  = table.Column<string>(maxLength: 50, nullable: false),
                    CommandNonce = table.Column<Guid>(nullable: false),
                    PackageId    = table.Column<Guid>(nullable: true),
                    Status       = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "Queued"),
                    Priority     = table.Column<byte>(nullable: false, defaultValue: (byte)5),
                    TTLMinutes   = table.Column<int>(nullable: false, defaultValue: 240),
                    ScheduledFor = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DispatchedAt = table.Column<DateTime>(nullable: true),
                    StartedAt    = table.Column<DateTime>(nullable: true),
                    CompletedAt  = table.Column<DateTime>(nullable: true),
                    RetryCount   = table.Column<byte>(nullable: false, defaultValue: (byte)0),
                    MaxRetries   = table.Column<byte>(nullable: false, defaultValue: (byte)3),
                    RetryAfter   = table.Column<DateTime>(nullable: true),
                    CreatedAt    = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedBy    = table.Column<string>(maxLength: 100, nullable: false, defaultValue: "SYSTEM"),
                },
                constraints: t =>
                {
                    t.PrimaryKey("PK_Commands", x => x.CommandId);
                    t.ForeignKey("FK_Commands_Terminal", x => x.TerminalId, "Terminals",       "TerminalId", onDelete: ReferentialAction.Restrict);
                    t.ForeignKey("FK_Commands_Store",    x => x.StoreId,    "Stores",           "StoreId",    onDelete: ReferentialAction.Restrict);
                    t.ForeignKey("FK_Commands_FYJob",    x => x.FYJobId,    "FinancialYearJobs","FYJobId");
                    t.ForeignKey("FK_Commands_Package",  x => x.PackageId,  "ScriptPackages",   "PackageId");
                });

            migrationBuilder.CreateIndex("UX_Commands_TerminalNonce",  "Commands", new[]{"TerminalId","CommandNonce"}, unique: true);
            migrationBuilder.CreateIndex("IX_Commands_Status",         "Commands", "Status", filter: "[Status] IN ('Queued','Dispatched','Running')");
            migrationBuilder.CreateIndex("IX_Commands_TerminalStatus", "Commands", new[]{"TerminalId","Status"});
            migrationBuilder.CreateIndex("IX_Commands_FYJobId",        "Commands", "FYJobId");

            migrationBuilder.CreateTable(
                name: "CommandExecutions",
                columns: table => new
                {
                    ExecutionId   = table.Column<Guid>(nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    CommandId     = table.Column<Guid>(nullable: false),
                    TerminalId    = table.Column<Guid>(nullable: false),
                    AttemptNumber = table.Column<byte>(nullable: false, defaultValue: (byte)1),
                    ExitCode      = table.Column<int>(nullable: true),
                    Stdout        = table.Column<string>(nullable: true),
                    Stderr        = table.Column<string>(nullable: true),
                    DurationMs    = table.Column<long>(nullable: true),
                    AgentVersion  = table.Column<string>(maxLength: 20, nullable: true),
                    StartedAt     = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CompletedAt   = table.Column<DateTime>(nullable: true),
                },
                constraints: t =>
                {
                    t.PrimaryKey("PK_CommandExecutions", x => x.ExecutionId);
                    t.ForeignKey("FK_Executions_Command", x => x.CommandId, "Commands", "CommandId", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex("IX_CE_CommandId",       "CommandExecutions", "CommandId");
            migrationBuilder.CreateIndex("IX_CE_TerminalStarted", "CommandExecutions", new[]{"TerminalId","StartedAt"});

            migrationBuilder.CreateTable(
                name: "Heartbeats",
                columns: table => new
                {
                    HeartbeatId       = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    TerminalId        = table.Column<Guid>(nullable: false),
                    ReceivedAt        = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    AgentVersion      = table.Column<string>(maxLength: 20, nullable: true),
                    Status            = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "ACTIVE"),
                    DiskFreeGB        = table.Column<decimal>(precision: 10, scale: 2, nullable: true),
                    PosProcessRunning = table.Column<bool>(nullable: false, defaultValue: false),
                    LocalTime         = table.Column<DateTime>(nullable: true),
                },
                constraints: t =>
                {
                    t.PrimaryKey("PK_Heartbeats", x => x.HeartbeatId);
                    t.ForeignKey("FK_Heartbeats_Terminal", x => x.TerminalId, "Terminals", "TerminalId", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex("IX_HB_TerminalReceived", "Heartbeats", new[]{"TerminalId","ReceivedAt"});

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    AuditId    = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    Timestamp  = table.Column<DateTime>(nullable: false, defaultValueSql: "GETUTCDATE()"),
                    UserId     = table.Column<string>(maxLength: 100, nullable: false),
                    IpAddress  = table.Column<string>(maxLength: 50, nullable: true),
                    Action     = table.Column<string>(maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(maxLength: 50, nullable: false),
                    EntityId   = table.Column<string>(maxLength: 100, nullable: false),
                    OldValue   = table.Column<string>(nullable: true),
                    NewValue   = table.Column<string>(nullable: true),
                    Result     = table.Column<string>(maxLength: 20, nullable: false, defaultValue: "SUCCESS"),
                    Details    = table.Column<string>(nullable: true),
                },
                constraints: t => t.PrimaryKey("PK_AuditLogs", x => x.AuditId));

            migrationBuilder.CreateIndex("IX_AL_Timestamp",  "AuditLogs", "Timestamp");
            migrationBuilder.CreateIndex("IX_AL_EntityType", "AuditLogs", new[]{"EntityType","EntityId"});
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("AuditLogs");
            migrationBuilder.DropTable("Heartbeats");
            migrationBuilder.DropTable("CommandExecutions");
            migrationBuilder.DropTable("Commands");
            migrationBuilder.DropTable("Terminals");
            migrationBuilder.DropTable("FinancialYearJobs");
            migrationBuilder.DropTable("ScriptPackages");
            migrationBuilder.DropTable("Stores");
        }
    }
}

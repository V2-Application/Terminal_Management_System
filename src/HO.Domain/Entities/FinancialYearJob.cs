using HO.Domain.Enums;

namespace HO.Domain.Entities;

public class FinancialYearJob
{
    public Guid FYJobId { get; set; } = Guid.NewGuid();
    public string FYYear { get; set; } = string.Empty;  // e.g. FY2025
    public FYJobStatus Status { get; set; } = FYJobStatus.Draft;
    public FYJobPhase Phase { get; set; } = FYJobPhase.PreValidation;
    public int WaveSize { get; set; } = 50;
    public int WaveIntervalMinutes { get; set; } = 5;
    public int? TotalStores { get; set; }
    public int CompletedStores { get; set; }
    public int FailedStores { get; set; }
    public int OfflineStores { get; set; }
    public Guid ScriptPackageId { get; set; }
    public Guid? RollbackPackageId { get; set; }
    public DateTime ExecutionWindowStart { get; set; }
    public DateTime ExecutionWindowEnd { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public string? SummaryReportPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

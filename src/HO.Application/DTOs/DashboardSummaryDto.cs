namespace HO.Application.DTOs;

public class DashboardSummaryDto
{
    public int TotalStores { get; set; }
    public int Completed { get; set; }
    public int Running { get; set; }
    public int Failed { get; set; }
    public int Offline { get; set; }
    public int Pending { get; set; }
    public int ActiveWave { get; set; }
    public double CompletionPct => TotalStores > 0 ? Math.Round((double)Completed / TotalStores * 100, 1) : 0;
    public string? ActiveFYJobId { get; set; }
    public string? ActiveFYYear { get; set; }
}

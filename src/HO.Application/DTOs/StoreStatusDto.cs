namespace HO.Application.DTOs;

public class StoreStatusDto
{
    public Guid StoreId { get; set; }
    public string StoreCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string FYCloseStatus { get; set; } = string.Empty;
    public string? CurrentStep { get; set; }
    public int? Progress { get; set; }
    public DateTime? LastUpdated { get; set; }
    public string? LastErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public int TerminalCount { get; set; }
    public bool HasActiveTerminal { get; set; }
}

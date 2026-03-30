namespace HO.Contracts.Responses;

public class PendingCommandsResponse
{
    public List<PendingCommandDto> Commands { get; set; } = new();
}

public class PendingCommandDto
{
    public Guid CommandId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public Guid? PackageId { get; set; }
    public string? PackageChecksum { get; set; }
    public string? Payload { get; set; }
    public int TtlMinutes { get; set; }
    public DateTime ScheduledFor { get; set; }
    public Guid CommandNonce { get; set; }
}

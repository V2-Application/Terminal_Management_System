namespace HO.Contracts.Requests;

public class RegisterTerminalRequest
{
    public string StoreCode { get; set; } = string.Empty;
    public string MachineId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string AgentVersion { get; set; } = string.Empty;
    public string? PosPath { get; set; }
    public string? IpAddress { get; set; }
}

public class HeartbeatRequest
{
    public Guid TerminalId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public decimal? DiskFreeGB { get; set; }
    public bool PosProcessRunning { get; set; }
    public string? AgentVersion { get; set; }
    public DateTime LocalTime { get; set; }
}

public class CommandAckRequest
{
    public Guid CommandId { get; set; }
    public Guid TerminalId { get; set; }
    public DateTime ReceivedAt { get; set; }
}

public class ExecutionStartRequest
{
    public Guid CommandId { get; set; }
    public Guid TerminalId { get; set; }
    public DateTime StartedAt { get; set; }
}

public class ExecutionResultRequest
{
    public Guid CommandId { get; set; }
    public Guid TerminalId { get; set; }
    public int ExitCode { get; set; }
    public string? Stdout { get; set; }
    public string? Stderr { get; set; }
    public long DurationMs { get; set; }
    public DateTime CompletedAt { get; set; }
}

public class RefreshTokenRequest
{
    public Guid TerminalId { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
}

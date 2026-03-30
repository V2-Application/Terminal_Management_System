namespace HO.Contracts.Responses;

public class RegisterTerminalResponse
{
    public Guid TerminalId { get; set; }
    public string AuthToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
}

public class HeartbeatResponse
{
    public DateTime ServerTime { get; set; }
    public int PendingCommandCount { get; set; }
}

public class TokenResponse
{
    public string AuthToken { get; set; } = string.Empty;
    public DateTime Expiry { get; set; }
}

public class CommandAckResponse
{
    public bool Acknowledged { get; set; } = true;
}

public class ExecutionResultResponse
{
    public Guid ResultId { get; set; }
    public bool RetryScheduled { get; set; }
}

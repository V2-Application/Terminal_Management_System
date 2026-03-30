namespace Store.Agent.Models;

/// <summary>
/// Stored in DPAPI-encrypted file. Never in appsettings.json.
/// </summary>
public class AgentCredentials
{
    public Guid TerminalId { get; set; }
    public string AuthToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
    public bool IsRegistered { get; set; }
}

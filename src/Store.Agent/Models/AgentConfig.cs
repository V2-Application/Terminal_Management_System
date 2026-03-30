namespace Store.Agent.Models;

/// <summary>
/// Configuration bound from appsettings.json.
/// NEVER put TerminalId or AuthToken here — those live in DPAPI-encrypted agent.credentials.json.
/// </summary>
public class AgentConfig
{
    public string HoApiBaseUrl { get; set; } = string.Empty;
    public string StoreCode { get; set; } = string.Empty;
    public int PollIntervalSeconds { get; set; } = 60;
    public int HeartbeatIntervalSeconds { get; set; } = 300;
    public int MaxConcurrentCommands { get; set; } = 1;
    public int DownloadTimeoutSeconds { get; set; } = 300;
    public int ExecutionTimeoutSeconds { get; set; } = 600;
    public string LocalPackageCacheDir { get; set; } = @"C:\RetailTMS\PackageCache";
    public string LocalLogDir { get; set; } = @"C:\RetailTMS\Logs";
    public string LocalDbPath { get; set; } = @"C:\RetailTMS\agent.db";
    public string PinnedCertThumbprint { get; set; } = string.Empty;
    public string PosExecutablePath { get; set; } =
        @"C:\Program Files (x86)\Microsoft Dynamics AX\60\Retail POS\POS.exe";
    public string PosExtensionsPath { get; set; } =
        @"C:\Program Files (x86)\Microsoft Dynamics AX\60\Retail POS\Extensions";
    public string IsolatedStoragePath { get; set; } =
        @"C:\ProgramData\IsolatedStorage";
}

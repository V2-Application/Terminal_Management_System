using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Store.Agent.Models;

namespace Store.Agent.Security;

/// <summary>
/// Stores terminal credentials encrypted with Windows DPAPI (machine scope).
/// The encrypted file CANNOT be decrypted on a different machine.
/// No plaintext credentials anywhere on disk.
/// </summary>
public class CredentialStore
{
    private readonly string _credFilePath;
    private readonly ILogger<CredentialStore> _logger;

    public CredentialStore(AgentConfig config, ILogger<CredentialStore> logger)
    {
        _credFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "RetailTMS", "agent.credentials.dat");
        _logger = logger;
        Directory.CreateDirectory(Path.GetDirectoryName(_credFilePath)!);
    }

    public async Task SaveAsync(AgentCredentials credentials)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(credentials);
        // DPAPI encrypt — machine scope so only this machine can decrypt
        var encrypted = ProtectedData.Protect(json, null, DataProtectionScope.LocalMachine);
        await File.WriteAllBytesAsync(_credFilePath, encrypted);
        _logger.LogInformation("Credentials saved (DPAPI encrypted) to {Path}", _credFilePath);
    }

    public async Task<AgentCredentials?> LoadAsync()
    {
        if (!File.Exists(_credFilePath)) return null;
        try
        {
            var encrypted = await File.ReadAllBytesAsync(_credFilePath);
            var json = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<AgentCredentials>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt credentials. File may be from different machine.");
            return null;
        }
    }

    public bool CredentialsExist() => File.Exists(_credFilePath);
}

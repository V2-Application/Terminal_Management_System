using System.Security.Cryptography;   // ProtectedData, DataProtectionScope
using System.Text;
using System.Text.Json;
using Store.Agent.Models;
using Microsoft.Extensions.Logging;

namespace Store.Agent.Security;

/// <summary>
/// Stores terminal credentials encrypted with Windows DPAPI (machine scope).
/// The encrypted file CANNOT be decrypted on a different machine.
/// No plaintext credentials ever written to disk.
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

    /// <summary>Encrypt credentials with DPAPI and write to disk.</summary>
    public async Task SaveAsync(AgentCredentials credentials)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(credentials);

        // DPAPI: LocalMachine scope — file cannot be decrypted on another machine
        var encrypted = ProtectedData.Protect(
            json,
            optionalEntropy: null,
            scope: DataProtectionScope.LocalMachine);

        await File.WriteAllBytesAsync(_credFilePath, encrypted);
        _logger.LogInformation("Credentials saved (DPAPI/LocalMachine encrypted) → {Path}", _credFilePath);
    }

    /// <summary>Load and decrypt credentials. Returns null if not found or decryption fails.</summary>
    public async Task<AgentCredentials?> LoadAsync()
    {
        if (!File.Exists(_credFilePath))
        {
            _logger.LogInformation("No credentials file at {Path} — terminal not yet registered", _credFilePath);
            return null;
        }

        try
        {
            var encrypted = await File.ReadAllBytesAsync(_credFilePath);
            var json = ProtectedData.Unprotect(
                encrypted,
                optionalEntropy: null,
                scope: DataProtectionScope.LocalMachine);

            return JsonSerializer.Deserialize<AgentCredentials>(json);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex,
                "DPAPI decryption failed — credentials were encrypted on a different machine or are corrupt.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading credentials");
            return null;
        }
    }

    public bool CredentialsExist() => File.Exists(_credFilePath);
}

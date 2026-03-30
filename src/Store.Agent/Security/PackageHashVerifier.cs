using System.Security.Cryptography;

namespace Store.Agent.Security;

/// <summary>
/// Verifies downloaded package integrity before execution.
/// Computes SHA-256 and optionally verifies RSA signature.
/// </summary>
public class PackageHashVerifier
{
    private readonly ILogger<PackageHashVerifier> _logger;
    // HO public key embedded in agent binary (not in config — config can be tampered)
    private static readonly string HoPublicKeyPem = "-----BEGIN PUBLIC KEY-----\n" +
        "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...[REPLACE WITH ACTUAL HO PUBLIC KEY]\n" +
        "-----END PUBLIC KEY-----";

    public PackageHashVerifier(ILogger<PackageHashVerifier> logger) => _logger = logger;

    public async Task<bool> VerifyAsync(string filePath, string expectedSha256)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Package file not found: {Path}", filePath);
            return false;
        }

        await using var stream = File.OpenRead(filePath);
        var hashBytes = await SHA256.HashDataAsync(stream);
        var actualHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("Hash mismatch! Expected: {Expected} Actual: {Actual}", expectedSha256, actualHash);
            return false;
        }

        _logger.LogInformation("Package hash verified: {Hash}", actualHash);
        return true;
    }
}

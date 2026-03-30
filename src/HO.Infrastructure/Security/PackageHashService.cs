using System.Security.Cryptography;

namespace HO.Infrastructure.Security;

public class PackageHashService
{
    public async Task<string> ComputeSha256Async(Stream fileStream)
    {
        fileStream.Position = 0;
        var bytes = await SHA256.HashDataAsync(fileStream);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool VerifyHash(string expectedHash, string actualHash)
        => string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase);
}

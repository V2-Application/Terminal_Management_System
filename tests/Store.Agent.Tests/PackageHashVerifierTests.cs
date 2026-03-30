using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Store.Agent.Security;
using System.Security.Cryptography;
using Xunit;

namespace Store.Agent.Tests;

public class PackageHashVerifierTests
{
    [Fact]
    public async Task VerifyAsync_ReturnsTrue_WhenHashMatches()
    {
        var verifier = new PackageHashVerifier(NullLogger<PackageHashVerifier>.Instance);
        var tempFile = Path.GetTempFileName();
        var content = "test package content"u8.ToArray();
        await File.WriteAllBytesAsync(tempFile, content);

        var expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

        var result = await verifier.VerifyAsync(tempFile, expectedHash);
        result.Should().BeTrue();

        File.Delete(tempFile);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsFalse_WhenHashMismatch()
    {
        var verifier = new PackageHashVerifier(NullLogger<PackageHashVerifier>.Instance);
        var tempFile = Path.GetTempFileName();
        await File.WriteAllBytesAsync(tempFile, "real content"u8.ToArray());

        var result = await verifier.VerifyAsync(tempFile, "0000000000000000000000000000000000000000000000000000000000000000");
        result.Should().BeFalse();

        File.Delete(tempFile);
    }
}

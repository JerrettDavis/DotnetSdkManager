using System.Security.Cryptography;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.UnitTests;

public sealed class HashVerifierTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sdk-manager-hash-{Guid.NewGuid():N}");

    public HashVerifierTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public async Task Accepts_hex_and_base64_sha512()
    {
        var path = Path.Combine(_directory, "payload");
        await File.WriteAllTextAsync(path, "payload");
        var bytes = SHA512.HashData("payload"u8.ToArray());

        await HashVerifier.VerifySha512Async(path, Convert.ToHexString(bytes));
        await HashVerifier.VerifySha512Async(path, Convert.ToBase64String(bytes));
    }

    [Fact]
    public async Task Mismatch_is_rejected()
    {
        var path = Path.Combine(_directory, "payload");
        await File.WriteAllTextAsync(path, "payload");
        var wrong = Convert.ToHexString(SHA512.HashData("different"u8.ToArray()));
        await Assert.ThrowsAsync<IntegrityException>(() => HashVerifier.VerifySha512Async(path, wrong));
    }

    [Fact]
    public void Missing_or_malformed_checksum_is_rejected()
    {
        Assert.Throws<IntegrityException>(() => HashVerifier.ParseExpected(string.Empty));
        Assert.Throws<IntegrityException>(() => HashVerifier.ParseExpected("not-a-hash"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

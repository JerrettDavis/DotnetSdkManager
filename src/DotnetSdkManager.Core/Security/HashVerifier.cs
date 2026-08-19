using System.Security.Cryptography;
using DotnetSdkManager.Exceptions;

namespace DotnetSdkManager.Security;

public static class HashVerifier
{
    public static async Task<string> VerifySha512Async(
        string filePath,
        string expected,
        CancellationToken cancellationToken = default)
    {
        var expectedBytes = ParseExpected(expected);
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha512 = SHA512.Create();
        var actualBytes = await sha512.ComputeHashAsync(stream, cancellationToken);

        if (!CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes))
        {
            throw new IntegrityException(
                $"SHA-512 mismatch for '{filePath}'. Expected {Convert.ToHexString(expectedBytes)}, actual {Convert.ToHexString(actualBytes)}.");
        }

        return Convert.ToHexString(actualBytes).ToLowerInvariant();
    }

    public static async Task<string> ComputeSha512Async(string filePath, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var sha512 = SHA512.Create();
        return Convert.ToHexString(await sha512.ComputeHashAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    public static byte[] ParseExpected(string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new IntegrityException("A SHA-512 checksum is required.");
        }

        var compact = string.Concat(expected.Where(character => !char.IsWhiteSpace(character) && character is not '-' and not ':'));
        if (compact.Length == 128 && compact.All(Uri.IsHexDigit))
        {
            return Convert.FromHexString(compact);
        }

        try
        {
            var decoded = Convert.FromBase64String(expected.Trim());
            if (decoded.Length == 64)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
        }

        throw new IntegrityException("The expected SHA-512 value must be 128 hexadecimal characters or a 64-byte Base64 value.");
    }
}

using System.Security.Cryptography;
using System.Text;
using DotnetSdkManager.IO;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.Http;

public sealed class ArtifactDownloader
{
    private readonly string _downloadCache;
    private readonly ValidatedHttpClient _http;

    public ArtifactDownloader(string downloadCache, ValidatedHttpClient http)
    {
        _downloadCache = downloadCache;
        _http = http;
    }

    public async Task<string> DownloadAsync(
        Uri uri,
        string advertisedFileName,
        string expectedSha512,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_downloadCache);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))).ToLowerInvariant();
        var extension = GetArchiveExtension(advertisedFileName);
        var destination = Path.Combine(_downloadCache, $"{key}{extension}");

        if (File.Exists(destination))
        {
            try
            {
                await HashVerifier.VerifySha512Async(destination, expectedSha512, cancellationToken);
                return destination;
            }
            catch (DotnetSdkManager.Exceptions.IntegrityException)
            {
                AtomicFile.TryDelete(destination);
            }
        }

        var temporary = Path.Combine(_downloadCache, $".{key}.{Guid.NewGuid():N}.part");
        try
        {
            using var response = await _http.GetAsync(uri, cancellationToken: cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var output = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
                await output.FlushAsync(cancellationToken);
                output.Flush(true);
            }

            await HashVerifier.VerifySha512Async(temporary, expectedSha512, cancellationToken);
            File.Move(temporary, destination, false);
            return destination;
        }
        finally
        {
            AtomicFile.TryDelete(temporary);
        }
    }

    private static string GetArchiveExtension(string fileName)
    {
        if (fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            return ".tar.gz";
        }

        if (fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            return ".tgz";
        }

        return Path.GetExtension(fileName).ToLowerInvariant();
    }
}

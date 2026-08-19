using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.IO;

namespace DotnetSdkManager.Http;

public sealed class MetadataCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _cacheDirectory;
    private readonly ValidatedHttpClient _http;

    public MetadataCache(string cacheDirectory, ValidatedHttpClient http)
    {
        _cacheDirectory = cacheDirectory;
        _http = http;
    }

    public async Task<string> GetStringAsync(Uri uri, bool offline, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_cacheDirectory);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(uri.AbsoluteUri))).ToLowerInvariant();
        var contentPath = Path.Combine(_cacheDirectory, $"{key}.json");
        var metadataPath = Path.Combine(_cacheDirectory, $"{key}.cache.json");
        var cacheMetadata = await ReadMetadataAsync(metadataPath, cancellationToken);

        if (offline)
        {
            return File.Exists(contentPath)
                ? await File.ReadAllTextAsync(contentPath, cancellationToken)
                : throw new ResolutionException($"Offline metadata cache does not contain '{uri}'.");
        }

        using var response = await _http.GetAsync(
            uri,
            headers => ApplyConditionalHeaders(headers, cacheMetadata),
            HttpCompletionOption.ResponseContentRead,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            if (!File.Exists(contentPath))
            {
                throw new ResolutionException($"Source returned 304 for '{uri}', but the cached body is missing.");
            }

            return await File.ReadAllTextAsync(contentPath, cancellationToken);
        }

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        _ = JsonDocument.Parse(content);

        var nextMetadata = new CacheMetadata(
            response.Headers.ETag?.ToString(),
            response.Content.Headers.LastModified ?? response.Headers.Date,
            DateTimeOffset.UtcNow,
            uri.AbsoluteUri);

        await AtomicFile.WriteAllTextAsync(contentPath, content, cancellationToken);
        await AtomicFile.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(nextMetadata, JsonOptions), cancellationToken);
        return content;
    }

    public void Clean()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }

        Directory.CreateDirectory(_cacheDirectory);
    }

    private static void ApplyConditionalHeaders(HttpRequestHeaders headers, CacheMetadata? metadata)
    {
        if (!string.IsNullOrWhiteSpace(metadata?.ETag) && EntityTagHeaderValue.TryParse(metadata.ETag, out var entityTag))
        {
            headers.IfNoneMatch.Add(entityTag);
        }

        if (metadata?.LastModified is { } lastModified)
        {
            headers.IfModifiedSince = lastModified;
        }
    }

    private static async Task<CacheMetadata?> ReadMetadataAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<CacheMetadata>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record CacheMetadata(
        string? ETag,
        DateTimeOffset? LastModified,
        DateTimeOffset SavedAtUtc,
        string Source);
}

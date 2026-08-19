using System.Globalization;
using System.Text.Json;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.Metadata;

public static class ReleaseMetadataParser
{
    public static IReadOnlyList<ReleaseChannel> ParseChannels(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("releases-index", out var releasesIndex) ||
            releasesIndex.ValueKind != JsonValueKind.Array)
        {
            throw new ResolutionException("The release index does not contain a 'releases-index' array.");
        }

        var result = new List<ReleaseChannel>();
        foreach (var element in releasesIndex.EnumerateArray())
        {
            var channelVersion = GetString(element, "channel-version");
            var releasesJson = GetString(element, "releases.json");
            if (string.IsNullOrWhiteSpace(channelVersion) ||
                string.IsNullOrWhiteSpace(releasesJson) ||
                !Uri.TryCreate(releasesJson, UriKind.Absolute, out var releasesUri))
            {
                continue;
            }

            result.Add(new ReleaseChannel(
                channelVersion,
                GetString(element, "latest-sdk"),
                GetString(element, "support-phase"),
                GetString(element, "release-type"),
                ParseDate(GetString(element, "eol-date")),
                releasesUri));
        }

        return result;
    }

    public static IReadOnlyList<SdkArtifact> ParseSdkArtifacts(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("releases", out var releases) ||
            releases.ValueKind != JsonValueKind.Array)
        {
            throw new ResolutionException("The channel metadata does not contain a 'releases' array.");
        }

        var artifacts = new List<SdkArtifact>();
        foreach (var release in releases.EnumerateArray())
        {
            var releaseDate = ParseDate(GetString(release, "release-date"));
            if (release.TryGetProperty("sdks", out var sdks) && sdks.ValueKind == JsonValueKind.Array)
            {
                foreach (var sdk in sdks.EnumerateArray())
                {
                    ParseSdk(sdk, releaseDate, artifacts);
                }
            }

            if (release.TryGetProperty("sdk", out var singularSdk) && singularSdk.ValueKind == JsonValueKind.Object)
            {
                ParseSdk(singularSdk, releaseDate, artifacts);
            }
        }

        return artifacts
            .GroupBy(
                artifact => $"{artifact.Version}\n{artifact.Rid}\n{artifact.Url.AbsoluteUri}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void ParseSdk(JsonElement sdk, DateOnly? releaseDate, List<SdkArtifact> artifacts)
    {
        var version = GetString(sdk, "version");
        if (string.IsNullOrWhiteSpace(version) || !DotnetSdkVersion.TryParse(version, out _))
        {
            return;
        }

        if (!sdk.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var file in files.EnumerateArray())
        {
            var rid = GetString(file, "rid");
            var url = GetString(file, "url");
            if (string.IsNullOrWhiteSpace(rid) ||
                string.IsNullOrWhiteSpace(url) ||
                !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var fileName = GetString(file, "name");
            if (string.IsNullOrWhiteSpace(fileName))
            {
                fileName = Path.GetFileName(uri.AbsolutePath);
            }

            if (!IsSdkArchive(fileName))
            {
                continue;
            }

            artifacts.Add(new SdkArtifact(
                version,
                rid,
                uri,
                GetString(file, "hash"),
                fileName,
                releaseDate));
        }
    }

    private static bool IsSdkArchive(string fileName) =>
        fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase) ||
        fileName.EndsWith(".tar", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;
}

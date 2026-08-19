using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Http;
using DotnetSdkManager.Models;
using DotnetSdkManager.Platform;

namespace DotnetSdkManager.Metadata;

public sealed class ReleaseCatalog
{
    private readonly Uri _releasesIndex;
    private readonly MetadataCache _cache;
    private readonly TimeProvider _timeProvider;

    public ReleaseCatalog(Uri releasesIndex, MetadataCache cache, TimeProvider? timeProvider = null)
    {
        _releasesIndex = releasesIndex;
        _cache = cache;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ReleaseChannel>> GetChannelsAsync(
        bool offline,
        CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(_releasesIndex, offline, cancellationToken);
        return ReleaseMetadataParser.ParseChannels(json);
    }

    public async Task<IReadOnlyList<SdkArtifact>> GetArtifactsAsync(
        ReleaseChannel channel,
        bool offline,
        CancellationToken cancellationToken = default)
    {
        var json = await _cache.GetStringAsync(channel.ReleasesJson, offline, cancellationToken);
        return ReleaseMetadataParser.ParseSdkArtifacts(json);
    }

    public async Task<ResolvedSdk> ResolveAsync(
        SdkResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var channels = await GetChannelsAsync(request.Offline, cancellationToken);
        var channel = SelectChannel(channels, request.Version, request.Channel, request.AllowEol, request.IncludePreview);
        var artifacts = await GetArtifactsAsync(channel, request.Offline, cancellationToken);
        var version = SelectVersion(artifacts, request.Version, request.IncludePreview);
        var versionArtifacts = artifacts
            .Where(artifact => string.Equals(artifact.Version, version.Original, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        SdkArtifact artifact;
        bool legacyFallback;
        try
        {
            (artifact, legacyFallback) = RidResolver.SelectArtifact(versionArtifacts, request.TargetRid, request.ArtifactRid);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResolutionException(exception.Message);
        }

        var isEol = channel.IsEol(GetToday());
        return new ResolvedSdk(
            version,
            channel.ChannelVersion,
            request.TargetRid,
            artifact.Rid,
            artifact.Url,
            artifact.Sha512,
            artifact.FileName,
            isEol,
            legacyFallback,
            channel.SupportPhase,
            channel.ReleaseType);
    }

    public async Task<IReadOnlyList<AvailableSdk>> ListAvailableAsync(
        string? requestedChannel,
        string targetRid,
        string? artifactRid,
        bool includePreview,
        bool allowEol,
        bool offline,
        CancellationToken cancellationToken = default)
    {
        var channels = await GetChannelsAsync(offline, cancellationToken);
        IReadOnlyList<ReleaseChannel> selectedChannels = string.IsNullOrWhiteSpace(requestedChannel)
            ? channels.Where(channel => allowEol || !channel.IsEol(GetToday())).ToArray()
            : [SelectChannel(channels, null, requestedChannel, allowEol, includePreview)];

        var result = new List<AvailableSdk>();
        foreach (var channel in selectedChannels)
        {
            if (!allowEol && channel.IsEol(GetToday()))
            {
                continue;
            }

            var artifacts = await GetArtifactsAsync(channel, offline, cancellationToken);
            foreach (var group in artifacts.GroupBy(item => item.Version, StringComparer.OrdinalIgnoreCase))
            {
                if (!DotnetSdkVersion.TryParse(group.Key, out var version) || (!includePreview && version.IsPrerelease))
                {
                    continue;
                }

                try
                {
                    var (artifact, fallback) = RidResolver.SelectArtifact(group, targetRid, artifactRid);
                    result.Add(new AvailableSdk(
                        version.Original,
                        channel.ChannelVersion,
                        targetRid,
                        artifact.Rid,
                        version.IsPrerelease,
                        channel.IsEol(GetToday()),
                        fallback,
                        channel.SupportPhase));
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return result
            .OrderByDescending(item => DotnetSdkVersion.Parse(item.Version))
            .ToArray();
    }

    private ReleaseChannel SelectChannel(
        IReadOnlyList<ReleaseChannel> channels,
        string? exactVersion,
        string? requestedChannel,
        bool allowEol,
        bool includePreview)
    {
        var selector = !string.IsNullOrWhiteSpace(exactVersion)
            ? DotnetSdkVersion.Parse(exactVersion).Channel
            : string.IsNullOrWhiteSpace(requestedChannel) ? "LTS" : requestedChannel.Trim();

        IEnumerable<ReleaseChannel> candidates;
        if (selector.Equals("LTS", StringComparison.OrdinalIgnoreCase))
        {
            candidates = channels.Where(channel => string.Equals(channel.ReleaseType, "lts", StringComparison.OrdinalIgnoreCase));
        }
        else if (selector.Equals("STS", StringComparison.OrdinalIgnoreCase) || selector.Equals("CURRENT", StringComparison.OrdinalIgnoreCase))
        {
            candidates = channels.Where(channel =>
                string.Equals(channel.ReleaseType, "sts", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(channel.ReleaseType, "current", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            candidates = channels.Where(channel => string.Equals(channel.ChannelVersion, selector, StringComparison.OrdinalIgnoreCase));
        }

        if (!allowEol)
        {
            candidates = candidates.Where(channel => !channel.IsEol(GetToday()));
        }

        if (!includePreview)
        {
            candidates = candidates.Where(channel => !channel.IsPreview);
        }

        var selected = candidates
            .OrderByDescending(channel => ParseChannelVersion(channel.ChannelVersion))
            .FirstOrDefault();

        if (selected is not null)
        {
            return selected;
        }

        var matchingWithoutPolicy = channels.FirstOrDefault(channel =>
            string.Equals(channel.ChannelVersion, selector, StringComparison.OrdinalIgnoreCase));
        if (matchingWithoutPolicy?.IsEol(GetToday()) == true && !allowEol)
        {
            throw new ResolutionException($"Channel '{selector}' is end-of-life. Repeat with --allow-eol to acknowledge the risk.");
        }

        if (matchingWithoutPolicy?.IsPreview == true && !includePreview)
        {
            throw new ResolutionException($"Channel '{selector}' is a preview or go-live channel. Repeat with --include-preview.");
        }

        throw new ResolutionException($"No release channel matched '{selector}' under the selected support policy.");
    }

    private static DotnetSdkVersion SelectVersion(
        IReadOnlyList<SdkArtifact> artifacts,
        string? exactVersion,
        bool includePreview)
    {
        var versions = artifacts
            .Select(artifact => DotnetSdkVersion.TryParse(artifact.Version, out var version) ? version : null)
            .Where(version => version is not null)
            .Cast<DotnetSdkVersion>()
            .Distinct()
            .ToArray();

        if (!string.IsNullOrWhiteSpace(exactVersion))
        {
            var requested = DotnetSdkVersion.Parse(exactVersion);
            if (requested.IsPrerelease && !includePreview)
            {
                throw new ResolutionException($"SDK '{exactVersion}' is a preview. Repeat with --include-preview.");
            }

            return versions.FirstOrDefault(version => string.Equals(version.Original, requested.Original, StringComparison.OrdinalIgnoreCase))
                ?? throw new ResolutionException($"SDK '{exactVersion}' was not found in release metadata.");
        }

        var selected = versions
            .Where(version => includePreview || !version.IsPrerelease)
            .OrderByDescending(version => version)
            .FirstOrDefault();
        return selected ?? throw new ResolutionException("No SDK version matched the requested preview policy.");
    }

    private DateOnly GetToday() => DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);

    private static DotnetSdkVersion ParseChannelVersion(string value) =>
        DotnetSdkVersion.TryParse(value, out var parsed) ? parsed : DotnetSdkVersion.Parse("0.0");
}

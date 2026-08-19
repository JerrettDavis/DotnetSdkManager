namespace DotnetSdkManager.Models;

public sealed record ReleaseChannel(
    string ChannelVersion,
    string? LatestSdk,
    string? SupportPhase,
    string? ReleaseType,
    DateOnly? EolDate,
    Uri ReleasesJson)
{
    public bool IsEol(DateOnly today) =>
        string.Equals(SupportPhase, "eol", StringComparison.OrdinalIgnoreCase) ||
        EolDate is { } eolDate && eolDate < today;

    public bool IsPreview =>
        string.Equals(SupportPhase, "preview", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(SupportPhase, "go-live", StringComparison.OrdinalIgnoreCase);
}

public sealed record SdkArtifact(
    string Version,
    string Rid,
    Uri Url,
    string? Sha512,
    string FileName,
    DateOnly? ReleaseDate = null);

public sealed record ResolvedSdk(
    DotnetSdkVersion Version,
    string Channel,
    string TargetRid,
    string ArtifactRid,
    Uri Url,
    string? Sha512,
    string FileName,
    bool IsEol,
    bool UsedLegacyRidFallback,
    string? SupportPhase,
    string? ReleaseType);

public sealed record AvailableSdk(
    string Version,
    string Channel,
    string TargetRid,
    string ArtifactRid,
    bool IsPrerelease,
    bool IsEol,
    bool UsesLegacyRidFallback,
    string? SupportPhase);

public sealed record SdkResolutionRequest(
    string? Version,
    string? Channel,
    string TargetRid,
    string? ArtifactRid,
    bool IncludePreview,
    bool AllowEol,
    bool Offline);

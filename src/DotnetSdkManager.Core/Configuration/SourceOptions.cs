namespace DotnetSdkManager.Configuration;

public sealed record SourceOptions(
    Uri ReleasesIndex,
    IReadOnlyCollection<string> AdditionalAllowedHosts,
    bool AllowHttp)
{
    public static readonly Uri OfficialReleasesIndex =
        new("https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/releases-index.json");

    public static SourceOptions Official => new(OfficialReleasesIndex, [], false);
}

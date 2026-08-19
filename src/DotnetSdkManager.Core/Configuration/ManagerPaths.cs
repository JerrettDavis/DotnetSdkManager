namespace DotnetSdkManager.Configuration;

public sealed class ManagerPaths
{
    public const string HomeEnvironmentVariable = "DOTNET_SDK_MANAGER_HOME";

    private ManagerPaths(string home)
    {
        Home = Path.GetFullPath(home);
    }

    public string Home { get; }

    public string Sdks => Path.Combine(Home, "sdks");

    public string Cache => Path.Combine(Home, "cache");

    public string MetadataCache => Path.Combine(Cache, "metadata");

    public string DownloadCache => Path.Combine(Cache, "downloads");

    public string Locks => Path.Combine(Home, "locks");

    public string Staging => Path.Combine(Home, "staging");

    public string Bin => Path.Combine(Home, "bin");

    public string ActiveFile => Path.Combine(Home, "active.json");

    public string CurrentRootFile => Path.Combine(Home, "current-root");

    public static ManagerPaths Create(string? overrideHome = null)
    {
        var home = FirstNonEmpty(
            overrideHome,
            Environment.GetEnvironmentVariable(HomeEnvironmentVariable),
            GetDefaultHome());

        return new ManagerPaths(Environment.ExpandEnvironmentVariables(home));
    }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Home);
        Directory.CreateDirectory(Sdks);
        Directory.CreateDirectory(MetadataCache);
        Directory.CreateDirectory(DownloadCache);
        Directory.CreateDirectory(Locks);
        Directory.CreateDirectory(Staging);
        Directory.CreateDirectory(Bin);
    }

    public string GetSdkRoot(string version, string targetRid) =>
        Path.Combine(Sdks, SanitizeSegment(version), SanitizeSegment(targetRid));

    public string GetManifestPath(string sdkRoot) => Path.Combine(sdkRoot, ".dotnet-sdk-manager.json");

    private static string GetDefaultHome()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(string.IsNullOrWhiteSpace(local) ? profile : local, "DotnetSdkManager");
        }

        var xdgData = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return !string.IsNullOrWhiteSpace(xdgData)
            ? Path.Combine(xdgData, "dotnet-sdk-manager")
            : Path.Combine(profile, ".dotnet-sdk-manager");
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.First(value => !string.IsNullOrWhiteSpace(value))!;

    private static string SanitizeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"'{value}' is not a safe path segment.", nameof(value));
        }

        return value;
    }
}

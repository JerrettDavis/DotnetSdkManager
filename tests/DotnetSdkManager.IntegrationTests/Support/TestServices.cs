using DotnetSdkManager.Archive;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Http;
using DotnetSdkManager.Metadata;
using DotnetSdkManager.Security;
using DotnetSdkManager.Services;

namespace DotnetSdkManager.IntegrationTests.Support;

internal sealed class TestServices : IDisposable
{
    private TestServices(
        ManagerPaths paths,
        ValidatedHttpClient http,
        ReleaseCatalog catalog,
        ArtifactDownloader downloader)
    {
        Paths = paths;
        Http = http;
        Catalog = catalog;
        Downloader = downloader;
        Installer = new SdkInstaller(paths, downloader, new SecureArchiveExtractor());
        Inventory = new InventoryService(paths);
        Activation = new ActivationService(paths, Inventory);
    }

    public ManagerPaths Paths { get; }

    public ValidatedHttpClient Http { get; }

    public ReleaseCatalog Catalog { get; }

    public ArtifactDownloader Downloader { get; }

    public SdkInstaller Installer { get; }

    public InventoryService Inventory { get; }

    public ActivationService Activation { get; }

    public static TestServices Create(string home, LoopbackFeed feed)
    {
        var paths = ManagerPaths.Create(home);
        paths.EnsureCreated();
        var source = new SourceOptions(feed.ReleasesIndex, ["127.0.0.1"], true);
        var http = new ValidatedHttpClient(new SourcePolicy(source));
        var cache = new MetadataCache(paths.MetadataCache, http);
        var catalog = new ReleaseCatalog(feed.ReleasesIndex, cache);
        return new TestServices(paths, http, catalog, new ArtifactDownloader(paths.DownloadCache, http));
    }

    public void Dispose() => Http.Dispose();
}

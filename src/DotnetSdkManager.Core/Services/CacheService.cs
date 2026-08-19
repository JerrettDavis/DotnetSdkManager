using DotnetSdkManager.Configuration;

namespace DotnetSdkManager.Services;

public sealed class CacheService
{
    private readonly ManagerPaths _paths;

    public CacheService(ManagerPaths paths)
    {
        _paths = paths;
    }

    public void Clean()
    {
        DeleteAndRecreate(_paths.MetadataCache);
        DeleteAndRecreate(_paths.DownloadCache);
    }

    private static void DeleteAndRecreate(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Directory.CreateDirectory(path);
    }
}

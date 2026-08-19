using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;

namespace DotnetSdkManager.Services;

public sealed class RemovalService
{
    private readonly ManagerPaths _paths;
    private readonly InventoryService _inventory;
    private readonly ActivationService _activation;

    public RemovalService(ManagerPaths paths, InventoryService inventory, ActivationService activation)
    {
        _paths = paths;
        _inventory = inventory;
        _activation = activation;
    }

    public async Task RemoveAsync(
        string version,
        string? targetRid,
        bool force,
        CancellationToken cancellationToken = default)
    {
        var managed = await _inventory.GetManagedAsync(cancellationToken);
        var matches = managed.Where(sdk =>
            string.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(targetRid) || string.Equals(sdk.TargetRid, targetRid, StringComparison.OrdinalIgnoreCase))).ToArray();
        if (matches.Length == 0)
        {
            throw new ResolutionException($"SDK '{version}' is not installed in the manager home.");
        }

        if (matches.Any(sdk => sdk.IsActive) && !force)
        {
            throw new InstallationException($"SDK '{version}' is active. Activate another SDK or repeat remove with --force.");
        }

        if (matches.Any(sdk => sdk.IsActive))
        {
            await _activation.ClearAsync(cancellationToken);
        }

        foreach (var sdk in matches)
        {
            EnsureManagedPath(sdk.Root);
            Directory.Delete(sdk.Root, recursive: true);
            var versionDirectory = Path.GetDirectoryName(sdk.Root)!;
            if (Directory.Exists(versionDirectory) && !Directory.EnumerateFileSystemEntries(versionDirectory).Any())
            {
                Directory.Delete(versionDirectory);
            }
        }
    }

    private void EnsureManagedPath(string path)
    {
        var root = Path.GetFullPath(_paths.Sdks).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!candidate.StartsWith(root, comparison))
        {
            throw new InstallationException($"Refusing to remove path outside the managed SDK root: '{candidate}'.");
        }
    }
}

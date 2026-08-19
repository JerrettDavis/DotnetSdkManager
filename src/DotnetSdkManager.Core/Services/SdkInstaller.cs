using System.Text.Json;
using DotnetSdkManager.Archive;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Http;
using DotnetSdkManager.IO;
using DotnetSdkManager.Models;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.Services;

public sealed class SdkInstaller
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ManagerPaths _paths;
    private readonly ArtifactDownloader _downloader;
    private readonly SecureArchiveExtractor _extractor;
    private readonly TimeProvider _timeProvider;

    public SdkInstaller(
        ManagerPaths paths,
        ArtifactDownloader downloader,
        SecureArchiveExtractor extractor,
        TimeProvider? timeProvider = null)
    {
        _paths = paths;
        _downloader = downloader;
        _extractor = extractor;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InstallResult> InstallAsync(
        ResolvedSdk resolved,
        string? localArchive,
        string? checksumOverride,
        bool force,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var expectedChecksum = checksumOverride ?? resolved.Sha512;
        if (string.IsNullOrWhiteSpace(expectedChecksum))
        {
            throw new IntegrityException(
                $"Release metadata for SDK {resolved.Version} does not include SHA-512. Supply a trusted checksum with --sha512.");
        }

        var lockName = $"{resolved.Version.Original}-{resolved.TargetRid}.lock";
        await using var installLock = await InstallationLock.AcquireAsync(
            Path.Combine(_paths.Locks, lockName),
            TimeSpan.FromMinutes(5),
            cancellationToken);

        var finalRoot = _paths.GetSdkRoot(resolved.Version.Original, resolved.TargetRid);
        if (Directory.Exists(finalRoot))
        {
            var existing = await TryReadManifestAsync(finalRoot, cancellationToken);
            if (!force && existing is not null &&
                string.Equals(existing.Sha512, NormalizeChecksum(expectedChecksum), StringComparison.OrdinalIgnoreCase) &&
                File.Exists(GetDotnetPath(finalRoot)))
            {
                return new InstallResult(ToManaged(existing, finalRoot, isActive: false), AlreadyInstalled: true);
            }

            if (!force)
            {
                throw new InstallationException(
                    $"SDK {resolved.Version} for {resolved.TargetRid} is already present but does not match the requested artifact. Use --force for an atomic replacement.");
            }
        }

        var archivePath = !string.IsNullOrWhiteSpace(localArchive)
            ? Path.GetFullPath(localArchive)
            : await _downloader.DownloadAsync(resolved.Url, resolved.FileName, expectedChecksum, cancellationToken);
        if (!File.Exists(archivePath))
        {
            throw new InstallationException($"SDK archive '{archivePath}' does not exist.");
        }

        var actualChecksum = await HashVerifier.VerifySha512Async(archivePath, expectedChecksum, cancellationToken);
        var stagingRoot = Path.Combine(
            _paths.Staging,
            $"{resolved.Version.Original}-{resolved.TargetRid}-{Guid.NewGuid():N}");
        var backupRoot = finalRoot + $".backup-{Guid.NewGuid():N}";
        var movedExisting = false;

        try
        {
            Directory.CreateDirectory(stagingRoot);
            await _extractor.ExtractAsync(archivePath, stagingRoot, resolved.FileName, cancellationToken);
            var dotnetPath = GetDotnetPath(stagingRoot);
            if (!File.Exists(dotnetPath))
            {
                throw new InstallationException(
                    $"Archive for SDK {resolved.Version} did not contain the expected dotnet host at its root.");
            }

            EnsureExecutable(dotnetPath);
            var manifest = new InstalledSdkManifest(
                resolved.Version.Original,
                resolved.Channel,
                resolved.TargetRid,
                resolved.ArtifactRid,
                localArchive is null ? resolved.Url.AbsoluteUri : Path.GetFullPath(localArchive),
                actualChecksum,
                _timeProvider.GetUtcNow(),
                resolved.IsEol,
                resolved.SupportPhase,
                resolved.ReleaseType);
            await AtomicFile.WriteAllTextAsync(
                _paths.GetManifestPath(stagingRoot),
                JsonSerializer.Serialize(manifest, JsonOptions),
                cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(finalRoot)!);
            if (Directory.Exists(finalRoot))
            {
                Directory.Move(finalRoot, backupRoot);
                movedExisting = true;
            }

            Directory.Move(stagingRoot, finalRoot);
            if (movedExisting)
            {
                TryDeleteDirectory(backupRoot);
            }

            return new InstallResult(ToManaged(manifest, finalRoot, isActive: false), AlreadyInstalled: false);
        }
        catch (SdkManagerException)
        {
            RestoreBackup(finalRoot, backupRoot, movedExisting);
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RestoreBackup(finalRoot, backupRoot, movedExisting);
            throw new InstallationException($"Unable to install SDK {resolved.Version}: {exception.Message}", exception);
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
        }
    }

    private static string NormalizeChecksum(string expected) =>
        Convert.ToHexString(HashVerifier.ParseExpected(expected)).ToLowerInvariant();

    private static string GetDotnetPath(string root) =>
        Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");

    private static void EnsureExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(path);
            File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        }
    }

    private static ManagedSdk ToManaged(InstalledSdkManifest manifest, string root, bool isActive) => new(
        manifest.Version,
        manifest.Channel,
        manifest.TargetRid,
        manifest.ArtifactRid,
        root,
        manifest.InstalledAtUtc,
        manifest.IsEol,
        isActive,
        manifest.Sha512);

    private async Task<InstalledSdkManifest?> TryReadManifestAsync(string root, CancellationToken cancellationToken)
    {
        var path = _paths.GetManifestPath(root);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstalledSdkManifest>(
                await File.ReadAllTextAsync(path, cancellationToken),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void RestoreBackup(string finalRoot, string backupRoot, bool movedExisting)
    {
        if (!movedExisting || !Directory.Exists(backupRoot))
        {
            return;
        }

        TryDeleteDirectory(finalRoot);
        Directory.Move(backupRoot, finalRoot);
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

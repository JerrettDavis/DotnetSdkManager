using System.Diagnostics;
using System.Text.Json;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.Services;

public sealed class InventoryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ManagerPaths _paths;

    public InventoryService(ManagerPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<ManagedSdk>> GetManagedAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var active = await GetActiveAsync(cancellationToken);
        var result = new List<ManagedSdk>();
        foreach (var manifestPath in Directory.EnumerateFiles(
                     _paths.Sdks,
                     ".dotnet-sdk-manager.json",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var manifest = JsonSerializer.Deserialize<InstalledSdkManifest>(
                    await File.ReadAllTextAsync(manifestPath, cancellationToken),
                    JsonOptions);
                if (manifest is null)
                {
                    continue;
                }

                var root = Path.GetDirectoryName(manifestPath)!;
                var isActive = active is not null && PathsEqual(root, active.Root);
                result.Add(new ManagedSdk(
                    manifest.Version,
                    manifest.Channel,
                    manifest.TargetRid,
                    manifest.ArtifactRid,
                    root,
                    manifest.InstalledAtUtc,
                    manifest.IsEol,
                    isActive,
                    manifest.Sha512));
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return result
            .OrderByDescending(sdk => DotnetSdkVersion.TryParse(sdk.Version, out var version) ? version : DotnetSdkVersion.Parse("0.0"))
            .ThenBy(sdk => sdk.TargetRid, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<SystemSdk>> GetSystemAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = FindSystemDotnet(),
                    ArgumentList = { "--list-sdks" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            if (!process.Start())
            {
                return [];
            }

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            if (process.ExitCode != 0)
            {
                return [];
            }

            var output = await outputTask;
            return ParseSystemSdkList(output)
                .Where(sdk => !IsUnderManagerHome(sdk.Root))
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return [];
        }
    }

    public async Task<ActiveSdk?> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.ActiveFile))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ActiveSdk>(
                await File.ReadAllTextAsync(_paths.ActiveFile, cancellationToken),
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private string FindSystemDotnet()
    {
        var executable = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                if (PathsEqual(entry, _paths.Bin))
                {
                    continue;
                }

                var candidate = Path.Combine(entry, executable);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return "dotnet";
    }

    private bool IsUnderManagerHome(string path)
    {
        try
        {
            var home = Path.GetFullPath(_paths.Home).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var candidate = Path.GetFullPath(path);
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return candidate.StartsWith(home, comparison);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    public static IReadOnlyList<SystemSdk> ParseSystemSdkList(string output)
    {
        var result = new List<SystemSdk>();
        foreach (var rawLine in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var opening = rawLine.LastIndexOf('[');
            var closing = rawLine.LastIndexOf(']');
            if (opening <= 0 || closing <= opening)
            {
                continue;
            }

            var version = rawLine[..opening].Trim();
            var root = rawLine[(opening + 1)..closing].Trim();
            if (!string.IsNullOrWhiteSpace(version) && !string.IsNullOrWhiteSpace(root))
            {
                result.Add(new SystemSdk(version, root));
            }
        }

        return result;
    }

    internal static bool PathsEqual(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            comparison);
    }
}

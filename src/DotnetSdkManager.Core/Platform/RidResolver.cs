using System.Runtime.InteropServices;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.Platform;

public static class RidResolver
{
    public static string GetCurrentRid()
    {
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            var value => value.ToString().ToLowerInvariant()
        };

        if (OperatingSystem.IsWindows())
        {
            return $"win-{architecture}";
        }

        if (OperatingSystem.IsMacOS())
        {
            return $"osx-{architecture}";
        }

        if (OperatingSystem.IsLinux())
        {
            var musl = File.Exists("/etc/alpine-release") ||
                       RuntimeInformation.RuntimeIdentifier.Contains("musl", StringComparison.OrdinalIgnoreCase);
            return musl ? $"linux-musl-{architecture}" : $"linux-{architecture}";
        }

        return RuntimeInformation.RuntimeIdentifier;
    }

    public static IReadOnlyList<string> GetCompatibleArtifactRids(string targetRid)
    {
        var candidates = new List<string> { targetRid };
        var parts = targetRid.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var architecture = parts.LastOrDefault() ?? "x64";

        if (targetRid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
        {
            Add(candidates,
                $"win10-{architecture}",
                $"win81-{architecture}",
                $"win8-{architecture}",
                $"win7-{architecture}");
        }
        else if (targetRid.StartsWith("osx-", StringComparison.OrdinalIgnoreCase))
        {
            Add(candidates,
                $"osx.13-{architecture}",
                $"osx.12-{architecture}",
                $"osx.11.0-{architecture}",
                $"osx.10.15-{architecture}",
                $"osx.10.14-{architecture}",
                $"osx.10.13-{architecture}",
                $"osx.10.12-{architecture}",
                $"osx.10.11-{architecture}");
        }
        else if (targetRid.StartsWith("linux-musl-", StringComparison.OrdinalIgnoreCase))
        {
            Add(candidates, $"alpine.3.18-{architecture}", $"alpine.3.17-{architecture}", $"alpine.3.16-{architecture}");
        }
        else if (targetRid.StartsWith("linux-", StringComparison.OrdinalIgnoreCase))
        {
            var osRelease = ReadOsRelease();
            if (osRelease.TryGetValue("ID", out var id))
            {
                switch (id.Trim('"').ToLowerInvariant())
                {
                    case "ubuntu":
                        Add(candidates,
                            $"ubuntu.24.04-{architecture}",
                            $"ubuntu.22.04-{architecture}",
                            $"ubuntu.20.04-{architecture}",
                            $"ubuntu.18.04-{architecture}",
                            $"ubuntu.16.04-{architecture}",
                            $"ubuntu.14.04-{architecture}");
                        break;
                    case "debian":
                        Add(candidates,
                            $"debian.12-{architecture}",
                            $"debian.11-{architecture}",
                            $"debian.10-{architecture}",
                            $"debian.9-{architecture}",
                            $"debian.8-{architecture}");
                        break;
                    case "rhel":
                    case "rocky":
                    case "almalinux":
                    case "centos":
                        Add(candidates,
                            $"rhel.9-{architecture}",
                            $"rhel.8-{architecture}",
                            $"rhel.7-{architecture}",
                            $"centos.7-{architecture}");
                        break;
                    case "fedora":
                        Add(candidates, $"fedora.38-{architecture}", $"fedora.27-{architecture}", $"fedora.24-{architecture}");
                        break;
                    case "opensuse-leap":
                    case "sles":
                        Add(candidates, $"opensuse.42.3-{architecture}", $"opensuse.42.1-{architecture}");
                        break;
                }
            }
        }

        return candidates;
    }

    public static (SdkArtifact Artifact, bool UsedLegacyFallback) SelectArtifact(
        IEnumerable<SdkArtifact> artifacts,
        string targetRid,
        string? exactArtifactRid = null)
    {
        var materialized = artifacts.ToArray();
        if (!string.IsNullOrWhiteSpace(exactArtifactRid))
        {
            var exact = materialized.FirstOrDefault(
                artifact => string.Equals(artifact.Rid, exactArtifactRid, StringComparison.OrdinalIgnoreCase));
            return exact is null
                ? throw new InvalidOperationException($"No SDK artifact exists for explicit RID '{exactArtifactRid}'.")
                : (exact, !string.Equals(targetRid, exact.Rid, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var candidate in GetCompatibleArtifactRids(targetRid))
        {
            var artifact = materialized.FirstOrDefault(
                item => string.Equals(item.Rid, candidate, StringComparison.OrdinalIgnoreCase));
            if (artifact is not null)
            {
                return (artifact, !string.Equals(candidate, targetRid, StringComparison.OrdinalIgnoreCase));
            }
        }

        var available = string.Join(", ", materialized.Select(item => item.Rid).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value));
        throw new InvalidOperationException($"No compatible artifact was found for target RID '{targetRid}'. Available RIDs: {available}.");
    }

    private static Dictionary<string, string> ReadOsRelease()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        const string path = "/etc/os-release";
        if (!File.Exists(path))
        {
            return result;
        }

        foreach (var line in File.ReadLines(path))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            result[line[..separator]] = line[(separator + 1)..];
        }

        return result;
    }

    private static void Add(List<string> values, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (!values.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(candidate);
            }
        }
    }
}

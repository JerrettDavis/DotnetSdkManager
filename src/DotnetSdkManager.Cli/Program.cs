using System.Net;
using System.Text.Json;
using DotnetSdkManager.Archive;
using DotnetSdkManager.Cli;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.Http;
using DotnetSdkManager.Metadata;
using DotnetSdkManager.Models;
using DotnetSdkManager.Platform;
using DotnetSdkManager.Security;
using DotnetSdkManager.Services;

return await ProgramEntry.RunAsync(args);

internal static class ProgramEntry
{
    public static async Task<int> RunAsync(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            var parsed = CliArguments.Parse(args);
            if (parsed.Has("version"))
            {
                Console.WriteLine("0.1.0");
                return 0;
            }

            if (parsed.Has("help") || string.IsNullOrWhiteSpace(parsed.Command) || parsed.Command == "help")
            {
                Console.WriteLine(HelpText.Text);
                return 0;
            }

            var paths = ManagerPaths.Create(parsed.Get("home"));
            paths.EnsureCreated();
            var sourceOptions = CreateSourceOptions(parsed);
            var sourcePolicy = new SourcePolicy(sourceOptions);
            using var http = new ValidatedHttpClient(sourcePolicy);
            var metadataCache = new MetadataCache(paths.MetadataCache, http);
            var catalog = new ReleaseCatalog(sourceOptions.ReleasesIndex, metadataCache);
            var downloader = new ArtifactDownloader(paths.DownloadCache, http);
            var inventory = new InventoryService(paths);
            var activation = new ActivationService(paths, inventory);

            return parsed.Command switch
            {
                "available" => await AvailableAsync(parsed, catalog, cancellation.Token),
                "install" => await InstallAsync(parsed, catalog, paths, downloader, activation, cancellation.Token),
                "upgrade" => await UpgradeAsync(parsed, catalog, paths, downloader, inventory, activation, cancellation.Token),
                "list" => await ListAsync(parsed, inventory, cancellation.Token),
                "activate" => await ActivateAsync(parsed, activation, cancellation.Token),
                "use" => await UseAsync(parsed, inventory, cancellation.Token),
                "remove" => await RemoveAsync(parsed, paths, inventory, activation, cancellation.Token),
                "env" => EnvCommand(parsed, paths),
                "doctor" => await DoctorAsync(parsed, paths, inventory, cancellation.Token),
                "cache" => Cache(parsed, paths),
                _ => throw new ArgumentException($"Unknown command '{parsed.Command}'. Run 'dotnet-sdk-manager help'.")
            };
        }
        catch (OperationCanceledException)
        {
            ConsoleOutput.Error("Operation cancelled.");
            return 130;
        }
        catch (SdkManagerException exception)
        {
            ConsoleOutput.Error(exception.Message);
            return exception.ExitCode;
        }
        catch (HttpRequestException exception)
        {
            ConsoleOutput.Error(exception.Message);
            return 5;
        }
        catch (JsonException exception)
        {
            ConsoleOutput.Error($"Invalid JSON: {exception.Message}");
            return 5;
        }
        catch (ArgumentException exception)
        {
            ConsoleOutput.Error(exception.Message);
            return 2;
        }
        catch (IOException exception)
        {
            ConsoleOutput.Error(exception.Message);
            return 8;
        }
    }

    private static async Task<int> AvailableAsync(
        CliArguments arguments,
        ReleaseCatalog catalog,
        CancellationToken cancellationToken)
    {
        var rid = arguments.Get("rid") ?? RidResolver.GetCurrentRid();
        var channel = arguments.Has("all-channels") ? null : arguments.Get("channel") ?? "LTS";
        var available = await catalog.ListAvailableAsync(
            channel,
            rid,
            arguments.Get("artifact-rid"),
            arguments.Has("include-preview"),
            arguments.Has("allow-eol"),
            arguments.Has("offline"),
            cancellationToken);

        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(available);
            return 0;
        }

        foreach (var sdk in available)
        {
            var flags = new List<string>();
            if (sdk.IsPrerelease)
            {
                flags.Add("preview");
            }

            if (sdk.IsEol)
            {
                flags.Add("EOL");
            }

            if (sdk.UsesLegacyRidFallback)
            {
                flags.Add($"artifact:{sdk.ArtifactRid}");
            }

            Console.WriteLine($"{sdk.Version,-20} {sdk.Channel,-8} {sdk.TargetRid,-18} {string.Join(", ", flags)}".TrimEnd());
        }

        return 0;
    }

    private static async Task<int> InstallAsync(
        CliArguments arguments,
        ReleaseCatalog catalog,
        ManagerPaths paths,
        ArtifactDownloader downloader,
        ActivationService activation,
        CancellationToken cancellationToken)
    {
        var exactVersion = arguments.Get("version") ?? arguments.Positionals.FirstOrDefault();
        var resolved = await ResolveInstallRequestAsync(arguments, catalog, exactVersion, cancellationToken);
        WarnForCompatibility(resolved);
        var installer = new SdkInstaller(paths, downloader, new SecureArchiveExtractor());
        var result = await installer.InstallAsync(
            resolved,
            arguments.Get("archive"),
            arguments.Get("sha512"),
            arguments.Has("force"),
            cancellationToken);

        ActiveSdk? active = null;
        if (arguments.Has("activate"))
        {
            active = await activation.ActivateAsync(result.Sdk.Version, result.Sdk.TargetRid, cancellationToken);
        }

        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(new { result.Sdk, result.AlreadyInstalled, Active = active });
        }
        else
        {
            Console.WriteLine(result.AlreadyInstalled
                ? $"SDK {result.Sdk.Version} is already installed at {result.Sdk.Root}."
                : $"Installed SDK {result.Sdk.Version} at {result.Sdk.Root}.");
            if (active is not null)
            {
                Console.WriteLine($"Activated SDK {active.Version}.");
            }
        }

        return 0;
    }

    private static async Task<int> UpgradeAsync(
        CliArguments arguments,
        ReleaseCatalog catalog,
        ManagerPaths paths,
        ArtifactDownloader downloader,
        InventoryService inventory,
        ActivationService activation,
        CancellationToken cancellationToken)
    {
        var channel = arguments.Get("channel");
        if (string.IsNullOrWhiteSpace(channel))
        {
            var active = await inventory.GetActiveAsync(cancellationToken);
            channel = active is null ? "LTS" : DotnetSdkVersion.Parse(active.Version).Channel;
        }

        var rid = arguments.Get("rid") ?? RidResolver.GetCurrentRid();
        var request = new SdkResolutionRequest(
            null,
            channel,
            rid,
            arguments.Get("artifact-rid"),
            arguments.Has("include-preview"),
            arguments.Has("allow-eol"),
            arguments.Has("offline"));
        var resolved = await catalog.ResolveAsync(request, cancellationToken);
        WarnForCompatibility(resolved);
        var installer = new SdkInstaller(paths, downloader, new SecureArchiveExtractor());
        var result = await installer.InstallAsync(
            resolved,
            localArchive: null,
            arguments.Get("sha512"),
            arguments.Has("force"),
            cancellationToken);

        ActiveSdk? activeSdk = null;
        if (!arguments.Has("no-activate"))
        {
            activeSdk = await activation.ActivateAsync(result.Sdk.Version, result.Sdk.TargetRid, cancellationToken);
        }

        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(new { result.Sdk, result.AlreadyInstalled, Active = activeSdk });
        }
        else
        {
            Console.WriteLine(result.AlreadyInstalled
                ? $"SDK {result.Sdk.Version} was already current for channel {channel}."
                : $"Upgraded channel {channel} to SDK {result.Sdk.Version}.");
            if (activeSdk is not null)
            {
                Console.WriteLine($"Activated SDK {activeSdk.Version}.");
            }
        }

        return 0;
    }

    private static async Task<int> ListAsync(
        CliArguments arguments,
        InventoryService inventory,
        CancellationToken cancellationToken)
    {
        var managed = await inventory.GetManagedAsync(cancellationToken);
        var system = arguments.Has("managed-only")
            ? Array.Empty<SystemSdk>()
            : await inventory.GetSystemAsync(cancellationToken);

        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(new { Managed = managed, System = system });
            return 0;
        }

        Console.WriteLine("Managed SDKs:");
        if (managed.Count == 0)
        {
            Console.WriteLine("  (none)");
        }
        else
        {
            foreach (var sdk in managed)
            {
                Console.WriteLine($"  {(sdk.IsActive ? "*" : " ")} {sdk.Version} [{sdk.TargetRid}; artifact {sdk.ArtifactRid}] {sdk.Root}");
            }
        }

        if (!arguments.Has("managed-only"))
        {
            Console.WriteLine("System/package-manager SDKs (read-only):");
            if (system.Count == 0)
            {
                Console.WriteLine("  (none discovered)");
            }
            else
            {
                foreach (var sdk in system)
                {
                    Console.WriteLine($"    {sdk.Version} [{sdk.Root}]");
                }
            }
        }

        return 0;
    }

    private static async Task<int> ActivateAsync(
        CliArguments arguments,
        ActivationService activation,
        CancellationToken cancellationToken)
    {
        var version = arguments.RequirePositional(0, "SDK version");
        var active = await activation.ActivateAsync(version, arguments.Get("rid"), cancellationToken);
        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(active);
        }
        else
        {
            Console.WriteLine($"Activated SDK {active.Version} at {active.Root}.");
        }

        return 0;
    }

    private static async Task<int> UseAsync(
        CliArguments arguments,
        InventoryService inventory,
        CancellationToken cancellationToken)
    {
        var version = arguments.RequirePositional(0, "SDK version");
        if (!arguments.Has("allow-uninstalled"))
        {
            var managed = await inventory.GetManagedAsync(cancellationToken);
            if (!managed.Any(sdk => string.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ResolutionException(
                    $"SDK '{version}' is not installed in the manager home. Install it first or pass --allow-uninstalled.");
            }
        }

        bool? allowPrerelease = arguments.Has("allow-prerelease")
            ? true
            : arguments.Has("no-allow-prerelease") ? false : null;
        var path = await new GlobalJsonService().WriteAsync(
            arguments.Get("path") ?? Environment.CurrentDirectory,
            version,
            arguments.Get("roll-forward"),
            allowPrerelease,
            cancellationToken);
        Console.WriteLine($"Pinned SDK {version} in {path}.");
        return 0;
    }

    private static async Task<int> RemoveAsync(
        CliArguments arguments,
        ManagerPaths paths,
        InventoryService inventory,
        ActivationService activation,
        CancellationToken cancellationToken)
    {
        var version = arguments.RequirePositional(0, "SDK version");
        await new RemovalService(paths, inventory, activation).RemoveAsync(
            version,
            arguments.Get("rid"),
            arguments.Has("force"),
            cancellationToken);
        Console.WriteLine($"Removed managed SDK {version}.");
        return 0;
    }

    private static int EnvCommand(CliArguments arguments, ManagerPaths paths)
    {
        Console.WriteLine(new EnvironmentService(paths).GetInstructions(arguments.Get("shell")));
        return 0;
    }

    private static async Task<int> DoctorAsync(
        CliArguments arguments,
        ManagerPaths paths,
        InventoryService inventory,
        CancellationToken cancellationToken)
    {
        var checks = await new DoctorService(paths, inventory).RunAsync(cancellationToken);
        if (arguments.Has("json"))
        {
            ConsoleOutput.Json(checks);
        }
        else
        {
            foreach (var check in checks)
            {
                Console.WriteLine($"{(check.Success ? "PASS" : "FAIL"),-4} {check.Name}: {check.Message}");
            }
        }

        return checks.All(check => check.Success) ? 0 : 1;
    }

    private static int Cache(CliArguments arguments, ManagerPaths paths)
    {
        var action = arguments.Positionals.FirstOrDefault();
        if (!string.Equals(action, "clean", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Usage: dotnet-sdk-manager cache clean");
        }

        new CacheService(paths).Clean();
        Console.WriteLine("Cleared metadata and download caches.");
        return 0;
    }

    private static async Task<ResolvedSdk> ResolveInstallRequestAsync(
        CliArguments arguments,
        ReleaseCatalog catalog,
        string? exactVersion,
        CancellationToken cancellationToken)
    {
        var targetRid = arguments.Get("rid") ?? RidResolver.GetCurrentRid();
        var artifactRid = arguments.Get("artifact-rid") ?? targetRid;
        var archive = arguments.Get("archive");
        var directUrl = arguments.Get("url");

        if (!string.IsNullOrWhiteSpace(archive) || !string.IsNullOrWhiteSpace(directUrl))
        {
            if (string.IsNullOrWhiteSpace(exactVersion))
            {
                throw new ArgumentException("A version is required with --archive or --url.");
            }

            if (string.IsNullOrWhiteSpace(arguments.Get("sha512")))
            {
                throw new IntegrityException("--archive and --url require a trusted --sha512 value.");
            }

            var version = DotnetSdkVersion.Parse(exactVersion);
            Uri uri;
            string fileName;
            if (!string.IsNullOrWhiteSpace(archive))
            {
                var fullPath = Path.GetFullPath(archive);
                uri = new Uri(fullPath);
                fileName = Path.GetFileName(fullPath);
            }
            else
            {
                uri = Uri.TryCreate(directUrl, UriKind.Absolute, out var parsed)
                    ? parsed
                    : throw new ArgumentException($"'{directUrl}' is not an absolute URI.");
                fileName = Path.GetFileName(uri.AbsolutePath);
            }

            return new ResolvedSdk(
                version,
                version.Channel,
                targetRid,
                artifactRid,
                uri,
                arguments.Get("sha512"),
                fileName,
                IsEol: false,
                UsedLegacyRidFallback: !string.Equals(targetRid, artifactRid, StringComparison.OrdinalIgnoreCase),
                SupportPhase: "manual",
                ReleaseType: "manual");
        }

        return await catalog.ResolveAsync(
            new SdkResolutionRequest(
                exactVersion,
                arguments.Get("channel"),
                targetRid,
                arguments.Get("artifact-rid"),
                arguments.Has("include-preview"),
                arguments.Has("allow-eol"),
                arguments.Has("offline")),
            cancellationToken);
    }

    private static SourceOptions CreateSourceOptions(CliArguments arguments)
    {
        var index = arguments.Get("index");
        var uri = string.IsNullOrWhiteSpace(index)
            ? SourceOptions.OfficialReleasesIndex
            : Uri.TryCreate(index, UriKind.Absolute, out var parsed)
                ? parsed
                : throw new ArgumentException($"'{index}' is not an absolute release-index URI.");
        return new SourceOptions(uri, arguments.GetAll("allow-host"), arguments.Has("allow-http"));
    }

    private static void WarnForCompatibility(ResolvedSdk resolved)
    {
        if (resolved.IsEol)
        {
            ConsoleOutput.Warning($"SDK {resolved.Version} is end-of-life and may contain known vulnerabilities.");
        }

        if (resolved.UsedLegacyRidFallback)
        {
            ConsoleOutput.Warning(
                $"Target RID '{resolved.TargetRid}' is using historical artifact RID '{resolved.ArtifactRid}'. Installation can succeed even when the old SDK cannot execute on this OS.");
        }
    }
}

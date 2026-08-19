using System.Diagnostics;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.Services;

public sealed class DoctorService
{
    private readonly ManagerPaths _paths;
    private readonly InventoryService _inventory;

    public DoctorService(ManagerPaths paths, InventoryService inventory)
    {
        _paths = paths;
        _inventory = inventory;
    }

    public async Task<IReadOnlyList<DoctorCheck>> RunAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        var checks = new List<DoctorCheck>();
        checks.Add(CheckWritableHome());

        var pathEntries = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        checks.Add(new DoctorCheck(
            "PATH",
            pathEntries.Any(entry => InventoryService.PathsEqual(entry, _paths.Bin)),
            pathEntries.Any(entry => InventoryService.PathsEqual(entry, _paths.Bin))
                ? $"Manager shim directory is on PATH: {_paths.Bin}"
                : $"Manager shim directory is not on PATH. Evaluate: dotnet-sdk-manager env"));

        var active = await _inventory.GetActiveAsync(cancellationToken);
        if (active is null)
        {
            checks.Add(new DoctorCheck("Active SDK", false, "No managed SDK is active."));
            return checks;
        }

        var dotnet = Path.Combine(active.Root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (!File.Exists(dotnet))
        {
            checks.Add(new DoctorCheck("Active SDK", false, $"Active root is missing its dotnet host: {dotnet}"));
            return checks;
        }

        checks.Add(new DoctorCheck("Active SDK", true, $"{active.Version} at {active.Root}"));
        checks.Add(await TryExecuteAsync(dotnet, active.Version, cancellationToken));
        return checks;
    }

    private DoctorCheck CheckWritableHome()
    {
        var probe = Path.Combine(_paths.Home, $".doctor-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return new DoctorCheck("Manager home", true, $"Writable: {_paths.Home}");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DoctorCheck("Manager home", false, exception.Message);
        }
    }

    private static async Task<DoctorCheck> TryExecuteAsync(
        string dotnet,
        string expectedVersion,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = dotnet,
                    ArgumentList = { "--version" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.Environment["DOTNET_MULTILEVEL_LOOKUP"] = "0";
            process.StartInfo.Environment["DOTNET_ROOT"] = Path.GetDirectoryName(dotnet);
            if (!process.Start())
            {
                return new DoctorCheck("SDK execution", false, "The dotnet host could not be started.");
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();
            return process.ExitCode == 0
                ? new DoctorCheck("SDK execution", true, $"dotnet --version returned '{output}'.")
                : new DoctorCheck(
                    "SDK execution",
                    false,
                    $"The installed archive is present, but SDK {expectedVersion} failed on this OS (exit {process.ExitCode}): {error}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new DoctorCheck("SDK execution", false, "The active SDK did not respond within 20 seconds.");
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return new DoctorCheck("SDK execution", false, exception.Message);
        }
    }
}

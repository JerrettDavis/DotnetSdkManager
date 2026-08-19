using System.Text.Json;
using DotnetSdkManager.Configuration;
using DotnetSdkManager.Exceptions;
using DotnetSdkManager.IO;
using DotnetSdkManager.Models;
using DotnetSdkManager.Platform;

namespace DotnetSdkManager.Services;

public sealed class ActivationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly ManagerPaths _paths;
    private readonly InventoryService _inventory;
    private readonly TimeProvider _timeProvider;

    public ActivationService(ManagerPaths paths, InventoryService inventory, TimeProvider? timeProvider = null)
    {
        _paths = paths;
        _inventory = inventory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ActiveSdk> ActivateAsync(
        string version,
        string? targetRid = null,
        CancellationToken cancellationToken = default)
    {
        _paths.EnsureCreated();
        targetRid ??= RidResolver.GetCurrentRid();
        var managed = await _inventory.GetManagedAsync(cancellationToken);
        var selected = managed.FirstOrDefault(sdk =>
                           string.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase) &&
                           string.Equals(sdk.TargetRid, targetRid, StringComparison.OrdinalIgnoreCase))
                       ?? managed.FirstOrDefault(sdk => string.Equals(sdk.Version, version, StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            throw new ResolutionException($"SDK '{version}' is not installed in the manager home.");
        }

        var dotnetPath = Path.Combine(selected.Root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
        if (!File.Exists(dotnetPath))
        {
            throw new InstallationException($"Installed SDK root '{selected.Root}' does not contain the dotnet host.");
        }

        var active = new ActiveSdk(selected.Version, selected.TargetRid, selected.Root, _timeProvider.GetUtcNow());
        await AtomicFile.WriteAllTextAsync(_paths.CurrentRootFile, selected.Root + Environment.NewLine, cancellationToken);
        await AtomicFile.WriteAllTextAsync(_paths.ActiveFile, JsonSerializer.Serialize(active, JsonOptions), cancellationToken);
        await WriteShimsAsync(cancellationToken);
        return active;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        AtomicFile.TryDelete(_paths.ActiveFile);
        AtomicFile.TryDelete(_paths.CurrentRootFile);
    }

    private async Task WriteShimsAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.Bin);
        var unixShim = Path.Combine(_paths.Bin, "dotnet");
        var windowsShim = Path.Combine(_paths.Bin, "dotnet.cmd");
        var powerShellShim = Path.Combine(_paths.Bin, "dotnet.ps1");
        var rootFileForShell = QuoteForSingleQuotedShell(_paths.CurrentRootFile);
        var unix = $$"""
            #!/bin/sh
            set -eu
            ROOT_FILE='{{rootFileForShell}}'
            if [ ! -r "$ROOT_FILE" ]; then
              echo "dotnet-sdk-manager: no active SDK; run 'dotnet-sdk-manager activate <version>'" >&2
              exit 127
            fi
            DOTNET_ROOT=$(cat "$ROOT_FILE")
            export DOTNET_ROOT
            export DOTNET_MULTILEVEL_LOOKUP=0
            exec "$DOTNET_ROOT/dotnet" "$@"
            """ + Environment.NewLine;
        await AtomicFile.WriteAllTextAsync(unixShim, unix, cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                unixShim,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        var rootFileForCmd = _paths.CurrentRootFile.Replace("%", "%%", StringComparison.Ordinal);
        var cmd = $$"""
            @echo off
            set "DOTNET_ROOT="
            set /p DOTNET_ROOT=<"{{rootFileForCmd}}"
            if not defined DOTNET_ROOT (
              echo dotnet-sdk-manager: no active SDK 1>&2
              exit /b 127
            )
            set DOTNET_MULTILEVEL_LOOKUP=0
            "%DOTNET_ROOT%\dotnet.exe" %*
            """ + Environment.NewLine;
        await AtomicFile.WriteAllTextAsync(windowsShim, cmd, cancellationToken);

        var rootFileForPowerShell = _paths.CurrentRootFile.Replace("'", "''", StringComparison.Ordinal);
        var powershell = $$"""
            $ErrorActionPreference = 'Stop'
            $env:DOTNET_ROOT = (Get-Content -LiteralPath '{{rootFileForPowerShell}}' -Raw).Trim()
            $env:DOTNET_MULTILEVEL_LOOKUP = '0'
            & (Join-Path $env:DOTNET_ROOT 'dotnet.exe') @args
            exit $LASTEXITCODE
            """ + Environment.NewLine;
        await AtomicFile.WriteAllTextAsync(powerShellShim, powershell, cancellationToken);
    }

    private static string QuoteForSingleQuotedShell(string value) => value.Replace("'", "'\\''", StringComparison.Ordinal);
}

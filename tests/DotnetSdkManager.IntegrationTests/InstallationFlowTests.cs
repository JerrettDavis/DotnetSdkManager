using DotnetSdkManager.IntegrationTests.Support;
using DotnetSdkManager.Models;

namespace DotnetSdkManager.IntegrationTests;

public sealed class InstallationFlowTests
{
    [Fact]
    public async Task Resolves_installs_activates_and_repeats_idempotently()
    {
        using var temp = new TemporaryDirectory("sdk-manager-integration");
        var rid = "linux-x64";
        await using var feed = await LoopbackFeed.CreateAsync(temp.Path, rid: rid);
        using var services = TestServices.Create(Path.Combine(temp.Path, "home"), feed);

        var resolved = await services.Catalog.ResolveAsync(new SdkResolutionRequest(
            "10.0.100",
            null,
            rid,
            null,
            IncludePreview: false,
            AllowEol: false,
            Offline: false));
        var first = await services.Installer.InstallAsync(resolved, null, null, force: false);
        Assert.False(first.AlreadyInstalled);
        Assert.True(File.Exists(Path.Combine(first.Sdk.Root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet")));

        var second = await services.Installer.InstallAsync(resolved, null, null, force: false);
        Assert.True(second.AlreadyInstalled);
        Assert.Equal(first.Sdk.Root, second.Sdk.Root);

        var active = await services.Activation.ActivateAsync("10.0.100", rid);
        Assert.Equal(first.Sdk.Root, active.Root);
        Assert.True(File.Exists(services.Paths.ActiveFile));
        Assert.True(File.Exists(Path.Combine(services.Paths.Bin, OperatingSystem.IsWindows() ? "dotnet.cmd" : "dotnet")));

        var inventory = await services.Inventory.GetManagedAsync();
        var installed = Assert.Single(inventory);
        Assert.True(installed.IsActive);
    }

    [Fact]
    public async Task Checksum_failure_leaves_no_final_install()
    {
        using var temp = new TemporaryDirectory("sdk-manager-checksum");
        const string rid = "linux-x64";
        await using var feed = await LoopbackFeed.CreateAsync(temp.Path, rid: rid, badChecksum: true);
        using var services = TestServices.Create(Path.Combine(temp.Path, "home"), feed);
        var resolved = await services.Catalog.ResolveAsync(new SdkResolutionRequest(
            "10.0.100", null, rid, null, false, false, false));

        await Assert.ThrowsAsync<DotnetSdkManager.Exceptions.IntegrityException>(
            () => services.Installer.InstallAsync(resolved, null, null, force: false));
        Assert.False(Directory.Exists(services.Paths.GetSdkRoot("10.0.100", rid)));
    }

    [Fact]
    public async Task Traversal_archive_leaves_no_install_and_no_escaped_file()
    {
        using var temp = new TemporaryDirectory("sdk-manager-traversal");
        const string rid = "linux-x64";
        await using var feed = await LoopbackFeed.CreateAsync(temp.Path, rid: rid, traversalArchive: true);
        using var services = TestServices.Create(Path.Combine(temp.Path, "home"), feed);
        var resolved = await services.Catalog.ResolveAsync(new SdkResolutionRequest(
            "10.0.100", null, rid, null, false, false, false));

        await Assert.ThrowsAsync<DotnetSdkManager.Exceptions.IntegrityException>(
            () => services.Installer.InstallAsync(resolved, null, null, force: false));
        Assert.False(Directory.Exists(services.Paths.GetSdkRoot("10.0.100", rid)));
        Assert.False(File.Exists(Path.Combine(services.Paths.Staging, "escaped.txt")));
    }
}

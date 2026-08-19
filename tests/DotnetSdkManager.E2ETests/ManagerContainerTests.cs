using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotnetSdkManager.E2ETests.Support;

namespace DotnetSdkManager.E2ETests;

public sealed class ManagerContainerTests
{
    [DockerFact]
    public async Task Feed_and_cli_containers_complete_legacy_install_activation_and_project_pin()
    {
        var repositoryRoot = RepositoryLocator.FindRoot();
        var cliOutput = RepositoryLocator.FindCliOutput(repositoryRoot);
        var workRoot = Path.Combine(Path.GetTempPath(), $"sdk-manager-e2e-{Guid.NewGuid():N}");
        var feedRoot = Path.Combine(workRoot, "feed");
        var stateRoot = Path.Combine(workRoot, "state");
        var projectRoot = Path.Combine(workRoot, "project");
        Directory.CreateDirectory(feedRoot);
        Directory.CreateDirectory(stateRoot);
        Directory.CreateDirectory(projectRoot);

        try
        {
            var artifactRid = await ContainerFeedBuilder.CreateAsync(feedRoot);
            await using var network = new NetworkBuilder()
                .WithName($"sdk-manager-e2e-{Guid.NewGuid():N}")
                .Build();
            await network.CreateAsync();

            await using var feed = new ContainerBuilder("nginx:1.27-alpine")
                .WithNetwork(network)
                .WithNetworkAliases("feed")
                .WithBindMount(feedRoot, "/usr/share/nginx/html", AccessMode.ReadOnly)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(80))
                .Build();

            await using var cli = new ContainerBuilder("mcr.microsoft.com/dotnet/sdk:10.0")
                .WithNetwork(network)
                .WithBindMount(cliOutput, "/app", AccessMode.ReadOnly)
                .WithBindMount(stateRoot, "/state", AccessMode.ReadWrite)
                .WithBindMount(projectRoot, "/workspace", AccessMode.ReadWrite)
                .WithEntrypoint("/bin/sh")
                .WithCommand("-c", "while true; do sleep 3600; done")
                .Build();

            await feed.StartAsync();
            await cli.StartAsync();

            var install = await cli.ExecAsync(
            [
                "dotnet", "/app/dotnet-sdk-manager.dll",
                "install", ContainerFeedBuilder.Version,
                "--index", "http://feed/release-metadata/releases-index.json",
                "--allow-host", "feed",
                "--allow-http",
                "--allow-eol",
                "--artifact-rid", artifactRid,
                "--activate",
                "--home", "/state/home"
            ]);
            Assert.True(install.ExitCode == 0, $"stdout: {install.Stdout}\nstderr: {install.Stderr}");
            Assert.Contains(artifactRid, install.Stderr, StringComparison.OrdinalIgnoreCase);

            var shim = await cli.ExecAsync(["/state/home/bin/dotnet", "--version"]);
            Assert.True(shim.ExitCode == 0, shim.Stderr);
            Assert.Equal(ContainerFeedBuilder.Version, shim.Stdout.Trim());

            var use = await cli.ExecAsync(
            [
                "dotnet", "/app/dotnet-sdk-manager.dll",
                "use", ContainerFeedBuilder.Version,
                "--path", "/workspace",
                "--roll-forward", "latestMajor",
                "--allow-prerelease",
                "--home", "/state/home"
            ]);
            Assert.True(use.ExitCode == 0, $"stdout: {use.Stdout}\nstderr: {use.Stderr}");

            var globalJsonPath = Path.Combine(projectRoot, "global.json");
            Assert.True(File.Exists(globalJsonPath));
            var globalJson = JsonNode.Parse(await File.ReadAllTextAsync(globalJsonPath))!.AsObject();
            var sdk = globalJson["sdk"]!.AsObject();
            Assert.Equal(ContainerFeedBuilder.Version, sdk["version"]!.GetValue<string>());
            Assert.False(sdk.ContainsKey("rollForward"));
            Assert.False(sdk.ContainsKey("allowPrerelease"));

            var list = await cli.ExecAsync(
            [
                "dotnet", "/app/dotnet-sdk-manager.dll",
                "list", "--managed-only", "--json", "--home", "/state/home"
            ]);
            Assert.True(list.ExitCode == 0, list.Stderr);
            Assert.Contains(ContainerFeedBuilder.Version, list.Stdout, StringComparison.Ordinal);
            Assert.Contains("\"isActive\": true", list.Stdout, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                if (Directory.Exists(workRoot))
                {
                    Directory.Delete(workRoot, recursive: true);
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
}

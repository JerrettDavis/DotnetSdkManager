using DotnetSdkManager.Models;
using DotnetSdkManager.Platform;

namespace DotnetSdkManager.UnitTests;

public sealed class RidResolverTests
{
    [Fact]
    public void Exact_rid_wins()
    {
        var artifacts = new[]
        {
            Artifact("win7-x64"),
            Artifact("win-x64")
        };

        var (selected, fallback) = RidResolver.SelectArtifact(artifacts, "win-x64");
        Assert.Equal("win-x64", selected.Rid);
        Assert.False(fallback);
    }

    [Fact]
    public void Historical_windows_rid_is_a_reported_fallback()
    {
        var (selected, fallback) = RidResolver.SelectArtifact([Artifact("win7-x64")], "win-x64");
        Assert.Equal("win7-x64", selected.Rid);
        Assert.True(fallback);
    }

    [Fact]
    public void Explicit_artifact_rid_is_deterministic()
    {
        var (selected, fallback) = RidResolver.SelectArtifact(
            [Artifact("linux-x64"), Artifact("ubuntu.16.04-x64")],
            "linux-x64",
            "ubuntu.16.04-x64");
        Assert.Equal("ubuntu.16.04-x64", selected.Rid);
        Assert.True(fallback);
    }

    [Fact]
    public void Musl_candidates_do_not_include_glibc_linux_rid()
    {
        var candidates = RidResolver.GetCompatibleArtifactRids("linux-musl-x64");
        Assert.DoesNotContain("linux-x64", candidates);
    }

    private static SdkArtifact Artifact(string rid) =>
        new("1.0.0", rid, new Uri($"https://example.test/{rid}.tar.gz"), "hash", $"{rid}.tar.gz");
}

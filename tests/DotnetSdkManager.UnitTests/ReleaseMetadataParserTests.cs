using DotnetSdkManager.Metadata;

namespace DotnetSdkManager.UnitTests;

public sealed class ReleaseMetadataParserTests
{
    [Fact]
    public void Parses_release_index_fields()
    {
        const string json = """
            {
              "releases-index": [
                {
                  "channel-version": "10.0",
                  "latest-sdk": "10.0.100",
                  "support-phase": "active",
                  "release-type": "lts",
                  "eol-date": "2028-11-14",
                  "releases.json": "https://example.test/10.0/releases.json"
                }
              ]
            }
            """;

        var channel = Assert.Single(ReleaseMetadataParser.ParseChannels(json));
        Assert.Equal("10.0", channel.ChannelVersion);
        Assert.Equal("lts", channel.ReleaseType);
        Assert.Equal(new DateOnly(2028, 11, 14), channel.EolDate);
    }

    [Fact]
    public void Parses_plural_and_historical_singular_sdk_shapes()
    {
        const string json = """
            {
              "releases": [
                {
                  "release-date": "2026-01-01",
                  "sdks": [
                    {
                      "version": "10.0.100",
                      "files": [
                        {
                          "name": "dotnet-sdk-10.0.100-linux-x64.tar.gz",
                          "rid": "linux-x64",
                          "url": "https://example.test/10.tar.gz",
                          "hash": "AA"
                        }
                      ]
                    }
                  ]
                },
                {
                  "release-date": "2018-01-01",
                  "sdk": {
                    "version": "2.0.3",
                    "files": [
                      {
                        "name": "dotnet-sdk-2.0.3-win7-x64.zip",
                        "rid": "win7-x64",
                        "url": "https://example.test/2.zip",
                        "hash": "BB"
                      }
                    ]
                  }
                }
              ]
            }
            """;

        var artifacts = ReleaseMetadataParser.ParseSdkArtifacts(json);
        Assert.Equal(2, artifacts.Count);
        Assert.Contains(artifacts, artifact => artifact.Version == "10.0.100" && artifact.Rid == "linux-x64");
        Assert.Contains(artifacts, artifact => artifact.Version == "2.0.3" && artifact.Rid == "win7-x64");
    }

    [Fact]
    public void Ignores_non_archive_files()
    {
        const string json = """
            {
              "releases": [
                {
                  "sdk": {
                    "version": "10.0.100",
                    "files": [
                      { "name": "checksums.txt", "rid": "linux-x64", "url": "https://example.test/checksums.txt", "hash": "AA" }
                    ]
                  }
                }
              ]
            }
            """;

        Assert.Empty(ReleaseMetadataParser.ParseSdkArtifacts(json));
    }
}

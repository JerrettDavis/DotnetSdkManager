using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using DotnetSdkManager.Security;

namespace DotnetSdkManager.E2ETests.Support;

internal static class ContainerFeedBuilder
{
    public const string Version = "2.1.818";

    public static async Task<string> CreateAsync(string root)
    {
        var architecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64";
        var artifactRid = $"ubuntu.16.04-{architecture}";
        var archiveName = $"dotnet-sdk-{Version}-{artifactRid}.zip";
        var archiveRelative = Path.Combine("archives", archiveName);
        var archivePath = Path.Combine(root, archiveRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

        await using (var file = new FileStream(archivePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("dotnet", CompressionLevel.NoCompression);
            entry.ExternalAttributes = (0x8000 | 0x1ED) << 16;
            await using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            await writer.WriteAsync($"#!/bin/sh\nif [ \"${{1:-}}\" = \"--version\" ]; then echo \"{Version}\"; else echo \"fake SDK {Version}\"; fi\n");
        }

        var hash = await HashVerifier.ComputeSha512Async(archivePath);
        var metadataRoot = Path.Combine(root, "release-metadata");
        var channelRoot = Path.Combine(metadataRoot, "2.1");
        Directory.CreateDirectory(channelRoot);
        await File.WriteAllTextAsync(
            Path.Combine(metadataRoot, "releases-index.json"),
            $$"""
            {
              "releases-index": [
                {
                  "channel-version": "2.1",
                  "latest-sdk": "{{Version}}",
                  "support-phase": "eol",
                  "release-type": "lts",
                  "eol-date": "2021-08-21",
                  "releases.json": "http://feed/release-metadata/2.1/releases.json"
                }
              ]
            }
            """);
        await File.WriteAllTextAsync(
            Path.Combine(channelRoot, "releases.json"),
            $$"""
            {
              "releases": [
                {
                  "release-date": "2021-08-21",
                  "sdk": {
                    "version": "{{Version}}",
                    "files": [
                      {
                        "name": "{{archiveName}}",
                        "rid": "{{artifactRid}}",
                        "url": "http://feed/archives/{{archiveName}}",
                        "hash": "{{hash}}"
                      }
                    ]
                  }
                }
              ]
            }
            """);

        return artifactRid;
    }
}

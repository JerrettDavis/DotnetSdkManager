using System.IO.Compression;
using DotnetSdkManager.Archive;
using DotnetSdkManager.Exceptions;

namespace DotnetSdkManager.UnitTests;

public sealed class SecureArchiveExtractorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"sdk-manager-archive-{Guid.NewGuid():N}");

    public SecureArchiveExtractorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("sub/../../outside")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    public void Unsafe_paths_are_rejected(string entry)
    {
        Assert.Throws<IntegrityException>(() => SecureArchiveExtractor.GetSafeTarget(_directory, entry));
    }

    [Fact]
    public async Task Zip_traversal_is_rejected_without_writing_outside_root()
    {
        var archivePath = Path.Combine(_directory, "bad.zip");
        using (var file = File.Create(archivePath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            await using var writer = new StreamWriter(archive.CreateEntry("../escaped.txt").Open());
            await writer.WriteAsync("bad");
        }

        var destination = Path.Combine(_directory, "extract");
        await Assert.ThrowsAsync<IntegrityException>(
            () => new SecureArchiveExtractor().ExtractAsync(archivePath, destination));
        Assert.False(File.Exists(Path.Combine(_directory, "escaped.txt")));
    }

    [Fact]
    public async Task Zip_symbolic_link_is_rejected()
    {
        var archivePath = Path.Combine(_directory, "link.zip");
        using (var file = File.Create(archivePath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("dotnet");
            entry.ExternalAttributes = (0xA000 | 0x1FF) << 16;
            await using var writer = new StreamWriter(entry.Open());
            await writer.WriteAsync("target");
        }

        await Assert.ThrowsAsync<IntegrityException>(
            () => new SecureArchiveExtractor().ExtractAsync(archivePath, Path.Combine(_directory, "extract")));
    }

    [Fact]
    public async Task Safe_zip_is_extracted()
    {
        var archivePath = Path.Combine(_directory, "safe.zip");
        using (var file = File.Create(archivePath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            await using var writer = new StreamWriter(archive.CreateEntry("sdk/marker.txt").Open());
            await writer.WriteAsync("ok");
        }

        var destination = Path.Combine(_directory, "extract");
        await new SecureArchiveExtractor().ExtractAsync(archivePath, destination);
        Assert.Equal("ok", await File.ReadAllTextAsync(Path.Combine(destination, "sdk", "marker.txt")));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}

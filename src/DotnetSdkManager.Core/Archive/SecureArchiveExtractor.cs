using System.Formats.Tar;
using System.IO.Compression;
using DotnetSdkManager.Exceptions;

namespace DotnetSdkManager.Archive;

public sealed class SecureArchiveExtractor
{
    public async Task ExtractAsync(
        string archivePath,
        string destination,
        string? advertisedFileName = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destination);
        var name = advertisedFileName ?? Path.GetFileName(archivePath);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            await ExtractZipAsync(archivePath, destination, cancellationToken);
            return;
        }

        if (name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase))
        {
            await using var file = OpenRead(archivePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress, leaveOpen: false);
            await ExtractTarAsync(gzip, destination, cancellationToken);
            return;
        }

        if (name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase))
        {
            await using var file = OpenRead(archivePath);
            await ExtractTarAsync(file, destination, cancellationToken);
            return;
        }

        throw new InstallationException($"Unsupported SDK archive type '{name}'. Expected .zip, .tar.gz, .tgz, or .tar.");
    }

    private static async Task ExtractZipAsync(string archivePath, string destination, CancellationToken cancellationToken)
    {
        await using var file = OpenRead(archivePath);
        using var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsZipSymbolicLink(entry))
            {
                throw new IntegrityException($"Archive entry '{entry.FullName}' is a symbolic link and is not permitted.");
            }

            var target = GetSafeTarget(destination, entry.FullName);
            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = entry.Open();
            await using var output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);

            if (!OperatingSystem.IsWindows())
            {
                var mode = (entry.ExternalAttributes >> 16) & 0x1FF;
                if (mode != 0)
                {
                    File.SetUnixFileMode(target, (UnixFileMode)mode);
                }
            }
        }
    }

    private static async Task ExtractTarAsync(Stream stream, string destination, CancellationToken cancellationToken)
    {
        using var reader = new TarReader(stream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry(copyData: false)) is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.EntryType is TarEntryType.GlobalExtendedAttributes or TarEntryType.ExtendedAttributes)
            {
                continue;
            }

            var target = GetSafeTarget(destination, entry.Name);
            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }

            if (entry.EntryType is not TarEntryType.RegularFile and
                not TarEntryType.V7RegularFile and
                not TarEntryType.ContiguousFile)
            {
                throw new IntegrityException(
                    $"Archive entry '{entry.Name}' has unsupported type '{entry.EntryType}'. Links and special files are not permitted.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var output = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (entry.DataStream is not null)
            {
                await entry.DataStream.CopyToAsync(output, cancellationToken);
            }

            await output.FlushAsync(cancellationToken);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(target, entry.Mode & (UnixFileMode)0x1FF);
            }
        }
    }

    internal static string GetSafeTarget(string destination, string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) || entryName.IndexOf('\0') >= 0)
        {
            throw new IntegrityException("Archive contains an empty or invalid path.");
        }

        var normalized = entryName.Replace('\\', '/');
        if (normalized.StartsWith('/') ||
            Path.IsPathRooted(normalized) ||
            (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':'))
        {
            throw new IntegrityException($"Archive path '{entryName}' is rooted and is not permitted.");
        }

        var destinationFull = Path.GetFullPath(destination);
        var target = Path.GetFullPath(Path.Combine(destinationFull, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = destinationFull.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!target.StartsWith(prefix, comparison) && !string.Equals(target, destinationFull, comparison))
        {
            throw new IntegrityException($"Archive path '{entryName}' escapes the extraction root.");
        }

        return target;
    }

    private static bool IsZipSymbolicLink(ZipArchiveEntry entry)
    {
        const int fileTypeMask = 0xF000;
        const int symbolicLink = 0xA000;
        var unixMode = (entry.ExternalAttributes >> 16) & fileTypeMask;
        return unixMode == symbolicLink;
    }

    private static FileStream OpenRead(string path) => new(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        128 * 1024,
        FileOptions.Asynchronous | FileOptions.SequentialScan);
}

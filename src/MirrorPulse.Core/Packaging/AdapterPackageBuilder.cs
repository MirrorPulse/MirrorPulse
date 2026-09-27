using System.IO.Compression;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterPackageBuildResult(string PackagePath, PackageFileManifest FileManifest);

/// <summary>
/// Creates a deterministic ZIP-based `.mpadapter` package from a prepared package directory.
/// </summary>
public static class AdapterPackageBuilder
{
    public static async Task<AdapterPackageBuildResult> BuildAsync(
        string sourceDirectory,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        var source = Path.GetFullPath(sourceDirectory);
        var output = Path.GetFullPath(outputPath);
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(source);
        }

        if (!string.Equals(Path.GetExtension(output), ".mpadapter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Adapter packages must use the .mpadapter extension.", nameof(outputPath));
        }

        if (IsUnderDirectory(output, source))
        {
            throw new ArgumentException("The package output cannot be inside its source directory.", nameof(outputPath));
        }

        var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .OrderBy(path => Path.GetRelativePath(source, path), StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var entries = new List<PackageFileEntry>(files.Length);
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            var relativePath = Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/');
            entries.Add(new PackageFileEntry(relativePath, content.LongLength, Sha256Digest.Compute(content)));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(output) ?? throw new InvalidOperationException("The package output has no parent directory."));
        await using (var stream = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/');
                var archiveEntry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);
                archiveEntry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                await using var input = File.OpenRead(file);
                await using var destination = archiveEntry.Open();
                await input.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            }
        }

        return new AdapterPackageBuildResult(output, new PackageFileManifest(entries));
    }

    private static bool IsUnderDirectory(string filePath, string directoryPath)
    {
        var directoryWithSeparator = directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return filePath.StartsWith(directoryWithSeparator, StringComparison.OrdinalIgnoreCase);
    }
}

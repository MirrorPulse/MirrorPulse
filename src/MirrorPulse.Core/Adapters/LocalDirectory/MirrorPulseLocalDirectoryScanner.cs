namespace MirrorPulse.Core.Adapters.LocalDirectory;

public enum MirrorPulseLocalDirectoryEntryKind
{
    File,
    Directory,
}

public sealed record MirrorPulseLocalDirectoryEntry(
    string RelativePath,
    MirrorPulseLocalDirectoryEntryKind Kind,
    long Length,
    DateTimeOffset LastWriteTimeUtc);

/// <summary>
/// Produces a deterministic snapshot of files and directories below a local source root.
/// </summary>
public static class MirrorPulseLocalDirectoryScanner
{
    public static IReadOnlyList<MirrorPulseLocalDirectoryEntry> Scan(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        var root = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(root);
        }

        var entries = new List<MirrorPulseLocalDirectoryEntry>();
        foreach (var directoryPath in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
        {
            var info = new DirectoryInfo(directoryPath);
            entries.Add(new MirrorPulseLocalDirectoryEntry(
                Normalize(Path.GetRelativePath(root, directoryPath)),
                MirrorPulseLocalDirectoryEntryKind.Directory,
                0,
                info.LastWriteTimeUtc));
        }

        foreach (var filePath in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(filePath);
            entries.Add(new MirrorPulseLocalDirectoryEntry(
                Normalize(Path.GetRelativePath(root, filePath)),
                MirrorPulseLocalDirectoryEntryKind.File,
                info.Length,
                info.LastWriteTimeUtc));
        }

        return entries
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ThenBy(entry => entry.Kind)
            .ToArray();
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
}

using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Adapters.Smb;

public sealed record MirrorPulseSmbEntry(
    string RelativePath,
    bool IsDirectory,
    long Length,
    DateTimeOffset LastWriteTimeUtc);

public enum MirrorPulseSmbChangeKind
{
    Added,
    Modified,
    Removed,
}

public sealed record MirrorPulseSmbChange(MirrorPulseSmbChangeKind Kind, MirrorPulseSmbEntry Entry);

public interface IMirrorPulseSmbEntrySource
{
    IEnumerable<MirrorPulseSmbEntry> Enumerate(string networkPath);
}

/// <summary>
/// Polls one SMB share and compares file metadata without relying on remote watcher support.
/// </summary>
public sealed class MirrorPulseSmbDirectoryPoller
{
    private readonly string _networkPath;
    private readonly IMirrorPulseSmbEntrySource _source;

    public MirrorPulseSmbDirectoryPoller(string networkPath, IMirrorPulseSmbEntrySource? source = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(networkPath);
        if (!networkPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("An SMB source must be a UNC path.", nameof(networkPath));
        }

        _networkPath = networkPath;
        _source = source ?? new WindowsSmbEntrySource();
    }

    public IReadOnlyList<MirrorPulseSmbEntry> Snapshot()
    {
        var entries = _source.Enumerate(_networkPath)
            .OrderBy(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (entries.Any(entry => string.IsNullOrWhiteSpace(entry.RelativePath)) ||
            entries.Select(entry => entry.RelativePath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length)
        {
            throw new InvalidDataException("The SMB scan contains invalid or duplicate relative paths.");
        }

        return new ReadOnlyCollection<MirrorPulseSmbEntry>(entries);
    }

    public static IReadOnlyList<MirrorPulseSmbChange> DetectChanges(
        IEnumerable<MirrorPulseSmbEntry> previous,
        IEnumerable<MirrorPulseSmbEntry> current)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var before = previous.ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
        var after = current.ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
        var changes = new List<MirrorPulseSmbChange>();
        foreach (var entry in after.Values)
        {
            if (!before.TryGetValue(entry.RelativePath, out var prior))
            {
                changes.Add(new(MirrorPulseSmbChangeKind.Added, entry));
            }
            else if (prior.IsDirectory != entry.IsDirectory ||
                     prior.Length != entry.Length ||
                     prior.LastWriteTimeUtc != entry.LastWriteTimeUtc)
            {
                changes.Add(new(MirrorPulseSmbChangeKind.Modified, entry));
            }
        }

        foreach (var entry in before.Values)
        {
            if (!after.ContainsKey(entry.RelativePath))
            {
                changes.Add(new(MirrorPulseSmbChangeKind.Removed, entry));
            }
        }

        return new ReadOnlyCollection<MirrorPulseSmbChange>(changes
            .OrderBy(change => change.Entry.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }

    private sealed class WindowsSmbEntrySource : IMirrorPulseSmbEntrySource
    {
        public IEnumerable<MirrorPulseSmbEntry> Enumerate(string networkPath)
        {
            foreach (var directory in Directory.EnumerateDirectories(networkPath, "*", SearchOption.AllDirectories))
            {
                var info = new DirectoryInfo(directory);
                yield return new(Normalize(Path.GetRelativePath(networkPath, directory)), true, 0, info.LastWriteTimeUtc);
            }

            foreach (var file in Directory.EnumerateFiles(networkPath, "*", SearchOption.AllDirectories))
            {
                var info = new FileInfo(file);
                yield return new(Normalize(Path.GetRelativePath(networkPath, file)), false, info.Length, info.LastWriteTimeUtc);
            }
        }

        private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
    }
}

using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Conflicts;

public enum MirrorPulseConflictPreservedSide
{
    Local,
    Remote,
}

/// <summary>Preserves one selected side in MP's private data root before a destructive decision.</summary>
public sealed class MirrorPulseConflictCopyStore
{
    private readonly MirrorPulseStoragePaths _paths;

    public MirrorPulseConflictCopyStore(MirrorPulseStoragePaths paths) =>
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));

    public async Task<string> PreserveAsync(
        MirrorPulseConflictRecord conflict,
        MirrorPulseConflictPreservedSide side,
        Func<CancellationToken, ValueTask<Stream>> openSource,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        ArgumentNullException.ThrowIfNull(openSource);
        if (!Enum.IsDefined(side))
        {
            throw new ArgumentOutOfRangeException(nameof(side), side, null);
        }

        string directory = Path.Combine(
            MirrorPulseConflictDirectory.GetPath(_paths, conflict.InstanceId),
            side.ToString().ToLowerInvariant());
        Directory.CreateDirectory(directory);
        EnsureNoReparsePoints(_paths.DataRootPath, directory);
        string destination = Path.Combine(directory, MirrorPulseConflictFileName.Create(conflict));
        if (File.Exists(destination))
        {
            return destination;
        }

        string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (Stream source = await openSource(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                await target.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            try
            {
                File.Move(temporary, destination, overwrite: false);
            }
            catch (IOException) when (File.Exists(destination))
            {
                // Another replay finished the same preserved copy first.
            }

            return destination;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public ValueTask<Stream> OpenLocalAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = Path.GetFullPath(Path.Combine(
            _paths.SyncRootPath,
            conflict.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (string.Equals(fullPath, _paths.SyncRootPath, StringComparison.OrdinalIgnoreCase) ||
            !SourceDirectoryPathNormalizer.IsWithin(_paths.SyncRootPath, fullPath))
        {
            throw new InvalidDataException("The local conflict path escapes the sync root.");
        }

        EnsureNoReparsePoints(_paths.SyncRootPath, fullPath);
        Stream stream = new FileStream(
            fullPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    }

    private static void EnsureNoReparsePoints(string root, string target)
    {
        string current = Path.GetFullPath(root);
        string fullTarget = Path.GetFullPath(target);
        if (!SourceDirectoryPathNormalizer.IsWithin(current, fullTarget))
        {
            throw new InvalidDataException("The conflict path escapes its storage root.");
        }

        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("A conflict path cannot traverse a reparse point.");
        }

        string relative = Path.GetRelativePath(current, fullTarget);
        foreach (string part in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
            {
                continue;
            }

            current = Path.Combine(current, part);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidDataException("A conflict path cannot traverse a reparse point.");
            }
        }
    }
}

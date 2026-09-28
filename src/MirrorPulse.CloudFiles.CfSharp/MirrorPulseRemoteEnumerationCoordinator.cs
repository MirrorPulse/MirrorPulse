using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseRemoteEnumerationResult(
    IReadOnlyList<CloudRemoteDirectoryEntry> Entries,
    ReadOnlyMemory<byte> FinalCursor,
    int PagesRead);

/// <summary>
/// Reads every deterministic remote-directory page while preserving opaque Adapter cursors.
/// </summary>
public sealed class MirrorPulseRemoteEnumerationCoordinator
{
    private readonly Func<CloudRemoteDirectoryQuery, CancellationToken, ValueTask<CloudRemoteDirectoryPage>> _readPage;

    public MirrorPulseRemoteEnumerationCoordinator(
        Func<CloudRemoteDirectoryQuery, CancellationToken, ValueTask<CloudRemoteDirectoryPage>> readPage)
    {
        ArgumentNullException.ThrowIfNull(readPage);
        _readPage = readPage;
    }

    [SupportedOSPlatform("windows10.0.16299")]
    public static MirrorPulseRemoteEnumerationCoordinator For(
        CloudFileSystem fileSystem,
        ICloudRemoteDirectoryCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(catalog);
        return new((query, cancellationToken) =>
            fileSystem.ReadRemoteDirectoryPageAsync(catalog, query, cancellationToken));
    }

    public async Task<MirrorPulseRemoteEnumerationResult> ReadAllAsync(
        string relativePath,
        int pageSize,
        CloudDirectoryEntryKinds entryKinds = CloudDirectoryEntryKinds.All,
        int maximumPages = 1024,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maximumPages, 0);

        var query = new CloudRemoteDirectoryQuery(relativePath, pageSize, ReadOnlyMemory<byte>.Empty, entryKinds);
        var entries = new List<CloudRemoteDirectoryEntry>();
        var seenCursors = new HashSet<string>(StringComparer.Ordinal);

        for (var pageNumber = 1; pageNumber <= maximumPages; pageNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await _readPage(query, cancellationToken).ConfigureAwait(false);
            entries.AddRange(page.Entries);
            if (page.IsComplete)
            {
                return new(entries.AsReadOnly(), page.ContinuationCursor, pageNumber);
            }

            if (page.ContinuationCursor.IsEmpty
                || !seenCursors.Add(Convert.ToBase64String(page.ContinuationCursor.Span)))
            {
                throw new InvalidDataException("The remote enumeration returned an empty or repeated continuation cursor.");
            }

            query = new CloudRemoteDirectoryQuery(relativePath, pageSize, page.ContinuationCursor, entryKinds);
        }

        throw new InvalidDataException("The remote enumeration exceeded its page limit.");
    }
}

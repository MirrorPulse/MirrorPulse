using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>Applies explicit upload-conflict decisions while preserving CfSharp's journal authority.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseUploadConflictActions
{
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly MirrorPulseCfSharpStateSession _state;
    private readonly CloudLocalChangeFeed _feed;
    private readonly MirrorPulseJournalUploadCompletion _completion;
    private readonly string _syncRootPath;
    private readonly string _conflictRootPath;

    public MirrorPulseUploadConflictActions(
        MirrorPulseProductCatalog catalog,
        MirrorPulseCfSharpStateSession state,
        CloudLocalChangeFeed feed,
        BackoffPolicy retryPolicy,
        MirrorPulseStoragePaths paths)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _completion = new MirrorPulseJournalUploadCompletion(feed, state, retryPolicy);
        _syncRootPath = Path.GetFullPath(paths.SyncRootPath);
        _conflictRootPath = Path.Combine(paths.DataRootPath, MirrorPulseConflictDirectory.DirectoryName);
    }

    public async ValueTask<MirrorPulseConflictResolution> ApplyAsync(
        Guid conflictId,
        MirrorPulseConflictAction action,
        CancellationToken cancellationToken = default)
    {
        MirrorPulseConflictRecord conflict = await _catalog.ReadUploadConflictAsync(
            conflictId, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException("The upload conflict was not found.");
        if (!conflict.IsPending)
        {
            throw new InvalidOperationException("The upload conflict has already been resolved.");
        }

        string? preservedPath = null;
        switch (action)
        {
            case MirrorPulseConflictAction.KeepLocal:
                await AlignAcknowledgedRevisionAsync(conflict, cancellationToken).ConfigureAwait(false);
                await _completion.PrepareRetryAsync(Guid.Parse(conflict.ChangeId), cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MirrorPulseConflictAction.Retry:
                await _completion.PrepareRetryAsync(Guid.Parse(conflict.ChangeId), cancellationToken)
                    .ConfigureAwait(false);
                break;
            case MirrorPulseConflictAction.KeepBoth:
                preservedPath = await PreserveLocalCopyAsync(conflict, cancellationToken)
                    .ConfigureAwait(false);
                await _feed.AcknowledgeAsync(
                    [new CloudLocalChangeAcknowledgement(Guid.Parse(conflict.ChangeId), null)],
                    cancellationToken).ConfigureAwait(false);
                break;
            case MirrorPulseConflictAction.KeepRemote:
            case MirrorPulseConflictAction.DeleteLocal:
                DeleteLocalCopy(conflict);
                await _feed.AcknowledgeAsync(
                    [new CloudLocalChangeAcknowledgement(Guid.Parse(conflict.ChangeId), null)],
                    cancellationToken).ConfigureAwait(false);
                break;
            case MirrorPulseConflictAction.DeleteRemote:
                throw new NotSupportedException(
                    "The Adapter protocol does not yet expose an atomic remote delete operation.");
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }

        await _catalog.SetUploadConflictStatusAsync(conflictId, MirrorPulseConflictStatus.Resolved,
            cancellationToken).ConfigureAwait(false);
        return new MirrorPulseConflictResolution(Guid.NewGuid(), conflictId, action,
            preservedPath, DateTimeOffset.UtcNow);
    }

    private async ValueTask AlignAcknowledgedRevisionAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conflict.RemoteRevision))
        {
            throw new InvalidOperationException("Keep-local requires a current remote revision.");
        }

        await using ICloudStateTransaction transaction = await _state.OpenStore
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        CloudItemState item = await transaction.Items.GetByRelativePathAsync(
            conflict.RelativePath, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException("The CfSharp item for the conflict was not found.");
        await transaction.Items.UpsertAsync(new CloudItemState(item.ItemId, item.RemoteId,
            item.RelativePath, item.Kind, conflict.RemoteRevision, item.LocalFileId,
            item.IsTombstone, DateTimeOffset.UtcNow), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<string> PreserveLocalCopyAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken)
    {
        string source = ResolveLocalPath(conflict.RelativePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The local conflict copy was not found.", source);
        }

        string directory = Path.Combine(_conflictRootPath, conflict.InstanceId.ToString());
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory,
            Path.GetFileName(source) + ".local-" + conflict.ConflictId.ToString("N"));
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read,
            64 * 1024, FileOptions.Asynchronous);
        await using FileStream output = new(target, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 64 * 1024, FileOptions.Asynchronous);
        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        return target;
    }

    private void DeleteLocalCopy(MirrorPulseConflictRecord conflict)
    {
        string path = ResolveLocalPath(conflict.RelativePath);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private string ResolveLocalPath(string relativePath)
    {
        string candidate = Path.GetFullPath(Path.Combine(_syncRootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = _syncRootPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The conflict path escaped the sync root.");
        }

        return candidate;
    }
}

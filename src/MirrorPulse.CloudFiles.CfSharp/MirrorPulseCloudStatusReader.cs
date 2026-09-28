using System.Security.Cryptography;
using CfSharp;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseInstanceCursorStatus(
    InstanceId InstanceId,
    bool HasCursor,
    string? CursorFingerprint,
    DateTimeOffset? UpdatedAt);

public sealed record MirrorPulseCloudStatusSnapshot(
    int PendingUploadCount,
    int PendingRemoteConflictCount,
    IReadOnlyList<MirrorPulseInstanceCursorStatus> Cursors);

/// <summary>Reads CfSharp's authoritative journal, conflicts and checkpoints for product status UI.</summary>
public static class MirrorPulseCloudStatusReader
{
    public static async Task<MirrorPulseCloudStatusSnapshot> ReadAsync(
        ICloudStateStore store,
        IEnumerable<InstanceId> instanceIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(instanceIds);
        InstanceId[] instances = instanceIds.Distinct().ToArray();
        await using ICloudStateTransaction transaction = await store.BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<CloudOperationJournalEntry> pending = await transaction.Operations
            .ListAsync(int.MaxValue, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CloudConflictState> conflicts = await transaction.Conflicts
            .ListAsync(cancellationToken).ConfigureAwait(false);
        var cursors = new List<MirrorPulseInstanceCursorStatus>(instances.Length);
        foreach (InstanceId instanceId in instances)
        {
            CloudStateCheckpoint? checkpoint = await transaction.Checkpoints.GetAsync(
                MirrorPulseRemoteBatchCoordinator.GetCheckpointName(instanceId), cancellationToken)
                .ConfigureAwait(false);
            cursors.Add(checkpoint is null
                ? new(instanceId, false, null, null)
                : new(instanceId, true,
                    Convert.ToHexString(SHA256.HashData(checkpoint.Value.Span))[..12],
                    checkpoint.UpdatedAt));
        }

        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return new(pending.Count, conflicts.Count, cursors);
    }
}

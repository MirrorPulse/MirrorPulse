using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Maintains a product-facing read model of CfSharp's durable remote conflicts. CfSharp remains
/// authoritative: a projection is shown only while its conflict ID exists in the official store.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseRemoteConflictProjector
{
    private readonly MirrorPulseCfSharpStateSession _state;
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly MirrorPulseConflictCenter _center;
    private readonly MirrorPulseConflictNotificationBridge _notifications;

    public MirrorPulseRemoteConflictProjector(
        MirrorPulseCfSharpStateSession state,
        MirrorPulseProductCatalog catalog,
        MirrorPulseConflictCenter center,
        MirrorPulseConflictNotificationBridge notifications)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _center = center ?? throw new ArgumentNullException(nameof(center));
        _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
    }

    public async Task CaptureResultAsync(
        InstanceId instanceId,
        CloudRemoteApplyResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        CloudRemoteConflict[] conflicts = result.Entries
            .Where(entry => entry.Conflict is not null)
            .Select(entry => entry.Conflict!)
            .ToArray();
        if (conflicts.Length != result.ConflictIds.Count)
        {
            throw new InvalidDataException("CfSharp conflict IDs and entry outcomes do not match.");
        }

        for (int index = 0; index < conflicts.Length; index++)
        {
            await CaptureAsync(instanceId, result.ConflictIds[index], conflicts[index], cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task CaptureAsync(
        InstanceId instanceId,
        Guid conflictId,
        CloudRemoteConflict conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        if (conflictId == Guid.Empty)
        {
            throw new ArgumentException("The CfSharp conflict ID cannot be empty.", nameof(conflictId));
        }

        await using (ICloudStateTransaction transaction = await _state.OpenStore
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false))
        {
            if (await transaction.Conflicts.GetAsync(conflictId, cancellationToken).ConfigureAwait(false) is null)
            {
                throw new InvalidDataException("The CfSharp conflict was not durable before projection.");
            }

            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        }

        var record = new MirrorPulseConflictRecord(
            conflictId,
            instanceId,
            conflict.Change.ChangeId,
            conflict.Change.RelativePath,
            MapReason(conflict.Reason),
            MirrorPulseVersionComparison.Unknown,
            null,
            conflict.Change.RemoteRevision,
            conflict.DetectedAt,
            source: MirrorPulseConflictSource.CfSharpRemote);
        await _catalog.SaveRemoteConflictProjectionAsync(record, cancellationToken).ConfigureAwait(false);
        bool isNew = !_center.Query(pendingOnly: false).Any(existing => existing.ConflictId == conflictId);
        _center.Upsert(record);
        if (isNew)
        {
            await _notifications.NotifyAsync(record, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Rebuilds pending UI state after restart and reports IDs lacking a product projection.</summary>
    public async Task<IReadOnlyList<Guid>> RestoreAsync(CancellationToken cancellationToken = default)
    {
        await using ICloudStateTransaction transaction = await _state.OpenStore
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<CloudConflictState> durable = await transaction.Conflicts
            .ListAsync(cancellationToken).ConfigureAwait(false);
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        Dictionary<Guid, MirrorPulseConflictRecord> projected = (await _catalog
                .ReadRemoteConflictProjectionsAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(record => record.ConflictId);
        HashSet<Guid> liveIds = durable.Select(conflict => conflict.ConflictId).ToHashSet();
        foreach (MirrorPulseConflictRecord existing in _center.Query(pendingOnly: false)
            .Where(record => record.Source == MirrorPulseConflictSource.CfSharpRemote && !liveIds.Contains(record.ConflictId)))
        {
            _center.Remove(existing.ConflictId);
        }

        var missing = new List<Guid>();
        foreach (CloudConflictState conflict in durable)
        {
            if (projected.TryGetValue(conflict.ConflictId, out MirrorPulseConflictRecord? record))
            {
                _center.Upsert(record);
            }
            else
            {
                missing.Add(conflict.ConflictId);
            }
        }

        return missing;
    }

    private static MirrorPulseConflictReason MapReason(CloudRemoteConflictReason reason) => reason switch
    {
        CloudRemoteConflictReason.Content => MirrorPulseConflictReason.Content,
        CloudRemoteConflictReason.Metadata => MirrorPulseConflictReason.Metadata,
        CloudRemoteConflictReason.Move => MirrorPulseConflictReason.Move,
        CloudRemoteConflictReason.Delete => MirrorPulseConflictReason.Delete,
        CloudRemoteConflictReason.PathCollision => MirrorPulseConflictReason.PathCollision,
        CloudRemoteConflictReason.StaleRemoteRevision => MirrorPulseConflictReason.StaleRemoteRevision,
        CloudRemoteConflictReason.MissingItem => MirrorPulseConflictReason.MissingItem,
        _ => MirrorPulseConflictReason.Unknown,
    };
}

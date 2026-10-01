using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Sync;

/// <summary>Repairs product state after an authoritative journal acknowledgement has committed.</summary>
public sealed class MirrorPulseMutationProjectionRecovery(MirrorPulseProductCatalog catalog)
{
    public async ValueTask<IReadOnlyList<Guid>> RepairAsync(
        Func<Guid, CancellationToken, ValueTask<bool>> isPending,
        Func<MirrorPulseMutationRecord, CancellationToken, ValueTask> project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isPending);
        ArgumentNullException.ThrowIfNull(project);
        var repaired = new List<Guid>();
        foreach (MirrorPulseMutationRecord record in await catalog.ReadIncompleteMutationsAsync(cancellationToken).ConfigureAwait(false))
        {
            if (record.Intent.Origin != MirrorPulseMutationOrigin.Journal || record.State != MirrorPulseMutationState.RemoteAccepted ||
                await isPending(record.Intent.OperationId, cancellationToken).ConfigureAwait(false)) continue;
            await project(record, cancellationToken).ConfigureAwait(false);
            await catalog.TransitionMutationAsync(record.Intent.OperationId, MirrorPulseMutationState.RemoteAccepted,
                MirrorPulseMutationState.Acknowledged, record.AcceptedRevision, cancellationToken).ConfigureAwait(false);
            repaired.Add(record.Intent.OperationId);
        }
        return repaired.AsReadOnly();
    }
}

namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Replays conflict resolutions exactly once and restores the center after a Host restart.
/// </summary>
public sealed class MirrorPulseConflictRecoveryCoordinator
{
    private readonly object _gate = new();
    private readonly MirrorPulseConflictCenter _center;
    private readonly Dictionary<Guid, MirrorPulseConflictResolutionResult> _resolutions = [];

    public MirrorPulseConflictRecoveryCoordinator(MirrorPulseConflictCenter center)
    {
        ArgumentNullException.ThrowIfNull(center);
        _center = center;
    }

    public MirrorPulseConflictResolutionResult Apply(
        Guid resolutionId,
        MirrorPulseConflictRecord conflict,
        Func<Guid, MirrorPulseConflictResolutionResult> apply)
    {
        if (resolutionId == Guid.Empty)
        {
            throw new ArgumentException("A resolution ID cannot be empty.", nameof(resolutionId));
        }

        ArgumentNullException.ThrowIfNull(conflict);
        ArgumentNullException.ThrowIfNull(apply);
        lock (_gate)
        {
            if (_resolutions.TryGetValue(resolutionId, out var existing))
            {
                if (existing.Resolution.ConflictId != conflict.ConflictId)
                {
                    throw new InvalidDataException("A resolution ID cannot be reused for another conflict.");
                }

                return existing;
            }

            var result = apply(resolutionId);
            ArgumentNullException.ThrowIfNull(result);
            if (result.Resolution.ResolutionId != resolutionId
                || result.Resolution.ConflictId != conflict.ConflictId)
            {
                throw new InvalidDataException("The conflict resolution result has mismatched identity.");
            }

            _resolutions.Add(resolutionId, result);
            _center.Upsert(result.UpdatedConflict);
            return result;
        }
    }

    public void Restore(IEnumerable<MirrorPulseConflictResolutionResult> resolutions)
    {
        ArgumentNullException.ThrowIfNull(resolutions);
        foreach (var result in resolutions)
        {
            ArgumentNullException.ThrowIfNull(result);
            lock (_gate)
            {
                if (_resolutions.TryGetValue(result.Resolution.ResolutionId, out var existing)
                    && existing.Resolution.ConflictId != result.Resolution.ConflictId)
                {
                    throw new InvalidDataException("Restored resolution IDs must remain conflict-scoped.");
                }

                _resolutions[result.Resolution.ResolutionId] = result;
                _center.Upsert(result.UpdatedConflict);
            }
        }
    }

    public IReadOnlyList<MirrorPulseConflictResolutionResult> Snapshot()
    {
        lock (_gate)
        {
            return _resolutions.Values.ToArray();
        }
    }
}

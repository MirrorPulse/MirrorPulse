namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Requeues a conflict without marking it resolved.
/// </summary>
public static class MirrorPulseConflictRetry
{
    public static MirrorPulseConflictResolutionResult Apply(
        MirrorPulseConflictRecord conflict,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        MirrorPulseConflictResolutionPlanner.Apply(
            conflict,
            MirrorPulseConflictAction.Retry,
            appliedAt,
            resolutionId: resolutionId);
}

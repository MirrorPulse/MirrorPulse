namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Applies an explicit destructive choice for one side of a conflict.
/// </summary>
public static class MirrorPulseConflictDeleteSide
{
    public static MirrorPulseConflictResolutionResult DeleteLocal(
        MirrorPulseConflictRecord conflict,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        Apply(conflict, MirrorPulseConflictAction.DeleteLocal, appliedAt, resolutionId);

    public static MirrorPulseConflictResolutionResult DeleteRemote(
        MirrorPulseConflictRecord conflict,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        Apply(conflict, MirrorPulseConflictAction.DeleteRemote, appliedAt, resolutionId);

    private static MirrorPulseConflictResolutionResult Apply(
        MirrorPulseConflictRecord conflict,
        MirrorPulseConflictAction action,
        DateTimeOffset appliedAt,
        Guid? resolutionId) =>
        MirrorPulseConflictResolutionPlanner.Apply(
            conflict,
            action,
            appliedAt,
            resolutionId: resolutionId);
}

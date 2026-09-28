namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Preserves the remote side at an explicit conflict path while retaining local content.
/// </summary>
public static class MirrorPulseConflictKeepBoth
{
    public static MirrorPulseConflictResolutionResult Apply(
        MirrorPulseConflictRecord conflict,
        string preservedPath,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        MirrorPulseConflictResolutionPlanner.Apply(
            conflict,
            MirrorPulseConflictAction.KeepBoth,
            appliedAt,
            preservedPath,
            resolutionId);
}

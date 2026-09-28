namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Applies the explicit KeepRemote decision after the caller's safety checks.
/// </summary>
public static class MirrorPulseConflictKeepRemote
{
    public static MirrorPulseConflictResolutionResult Apply(
        MirrorPulseConflictRecord conflict,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        MirrorPulseConflictResolutionPlanner.Apply(
            conflict,
            MirrorPulseConflictAction.KeepRemote,
            appliedAt,
            resolutionId: resolutionId);
}

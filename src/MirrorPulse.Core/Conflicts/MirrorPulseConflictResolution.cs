using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Conflicts;

public enum MirrorPulseConflictAction
{
    KeepLocal,
    KeepRemote,
    KeepBoth,
    Retry,
    DeleteLocal,
    DeleteRemote,
    Defer,
}

public sealed record MirrorPulseConflictResolution(
    Guid ResolutionId,
    Guid ConflictId,
    MirrorPulseConflictAction Action,
    string? PreservedPath,
    DateTimeOffset AppliedAt);

public sealed record MirrorPulseConflictResolutionResult(
    MirrorPulseConflictResolution Resolution,
    MirrorPulseConflictRecord UpdatedConflict);

/// <summary>
/// Validates a conflict action and returns the durable resolution plus updated center record.
/// </summary>
public static class MirrorPulseConflictResolutionPlanner
{
    public static MirrorPulseConflictResolutionResult Apply(
        MirrorPulseConflictRecord conflict,
        MirrorPulseConflictAction action,
        DateTimeOffset appliedAt,
        string? preservedPath = null,
        Guid? resolutionId = null)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        if (!conflict.IsPending)
        {
            throw new InvalidOperationException("Only pending conflicts can receive a resolution action.");
        }

        if (action == MirrorPulseConflictAction.KeepBoth)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(preservedPath);
        }
        else if (preservedPath is not null)
        {
            throw new ArgumentException("Only KeepBoth may specify a preserved path.", nameof(preservedPath));
        }

        var resolution = new MirrorPulseConflictResolution(
            resolutionId ?? Guid.NewGuid(),
            conflict.ConflictId,
            action,
            preservedPath,
            appliedAt);
        var status = action is MirrorPulseConflictAction.Retry or MirrorPulseConflictAction.Defer
            ? MirrorPulseConflictStatus.Pending
            : MirrorPulseConflictStatus.Resolved;
        var updated = new MirrorPulseConflictRecord(
            conflict.ConflictId,
            conflict.InstanceId,
            conflict.ChangeId,
            conflict.RelativePath,
            conflict.Reason,
            conflict.VersionComparison,
            conflict.LocalRevision,
            conflict.RemoteRevision,
            conflict.DetectedAt,
            status,
            conflict.Source);
        return new(resolution, updated);
    }

    public static MirrorPulseConflictResolutionResult KeepLocal(
        MirrorPulseConflictRecord conflict,
        DateTimeOffset appliedAt,
        Guid? resolutionId = null) =>
        Apply(conflict, MirrorPulseConflictAction.KeepLocal, appliedAt, resolutionId: resolutionId);
}

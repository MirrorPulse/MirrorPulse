using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Conflicts;

namespace MirrorPulse.Core.Sync;

[SupportedOSPlatform("windows10.0.16299")]
public sealed record MirrorPulseEndToEndSyncSimulationResult(
    MirrorPulseSyncReplayResult Replay,
    IReadOnlyList<Guid> PendingConflictIds,
    int NotificationCount);

/// <summary>
/// Exercises the local journal plan, remote replay, conflict center, and notification path together.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseEndToEndSyncSimulation
{
    public static async Task<MirrorPulseEndToEndSyncSimulationResult> RunAsync(
        IEnumerable<MirrorPulseWorkerChangeCommand> localOperations,
        IEnumerable<MirrorPulseRemoteOperation> remoteOperations,
        IEnumerable<MirrorPulseConflictRecord> conflicts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localOperations);
        ArgumentNullException.ThrowIfNull(remoteOperations);
        ArgumentNullException.ThrowIfNull(conflicts);
        cancellationToken.ThrowIfCancellationRequested();

        var replay = MirrorPulseSyncReplayHarness.Replay(localOperations, remoteOperations);
        var center = new MirrorPulseConflictCenter();
        var notificationCount = 0;
        var bridge = new MirrorPulseConflictNotificationBridge((_, token) =>
        {
            token.ThrowIfCancellationRequested();
            notificationCount++;
            return ValueTask.CompletedTask;
        });

        foreach (var conflict in conflicts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            center.Upsert(conflict);
            if (conflict.IsPending)
            {
                await bridge.NotifyAsync(conflict, cancellationToken).ConfigureAwait(false);
            }
        }

        var pendingConflictIds = center.Query()
            .Select(conflict => conflict.ConflictId)
            .ToArray();
        return new(
            replay,
            new ReadOnlyCollection<Guid>(pendingConflictIds),
            notificationCount);
    }
}

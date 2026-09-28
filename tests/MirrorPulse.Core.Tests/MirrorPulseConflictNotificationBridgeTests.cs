using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictNotificationBridgeTests
{
    [TestMethod]
    public async Task BridgeNotifiesImmediatelyWithThePendingConflict()
    {
        MirrorPulseConflictRecord? received = null;
        var bridge = new MirrorPulseConflictNotificationBridge((conflict, _) =>
        {
            received = conflict;
            return ValueTask.CompletedTask;
        });
        var conflict = Create();

        await bridge.NotifyAsync(conflict);

        Assert.AreSame(conflict, received);
    }

    [TestMethod]
    public async Task BridgeRejectsResolvedConflicts()
    {
        var bridge = new MirrorPulseConflictNotificationBridge((_, _) => ValueTask.CompletedTask);
        var resolved = new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "change",
            "file.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local",
            "remote",
            DateTimeOffset.UtcNow,
            MirrorPulseConflictStatus.Resolved);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => bridge.NotifyAsync(resolved).AsTask());
    }

    private static MirrorPulseConflictRecord Create() => new(
        Guid.NewGuid(),
        InstanceId.New(),
        "change",
        "file.txt",
        MirrorPulseConflictReason.Content,
        MirrorPulseVersionComparison.Diverged,
        "local",
        "remote",
        DateTimeOffset.UtcNow);
}

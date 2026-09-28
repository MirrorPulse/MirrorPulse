using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictRecoveryCoordinatorTests
{
    [TestMethod]
    public void CoordinatorMakesResolutionIdempotentAndUpdatesCenter()
    {
        var center = new MirrorPulseConflictCenter();
        var coordinator = new MirrorPulseConflictRecoveryCoordinator(center);
        var conflict = Create();
        var resolutionId = Guid.NewGuid();
        var callbackCount = 0;

        var first = coordinator.Apply(resolutionId, conflict, id =>
        {
            callbackCount++;
            return MirrorPulseConflictResolutionPlanner.KeepLocal(conflict, DateTimeOffset.UtcNow, id);
        });
        var second = coordinator.Apply(resolutionId, conflict, id =>
        {
            callbackCount++;
            return MirrorPulseConflictKeepRemote.Apply(conflict, DateTimeOffset.UtcNow, id);
        });

        Assert.AreSame(first, second);
        Assert.AreEqual(1, callbackCount);
        Assert.AreEqual(MirrorPulseConflictAction.KeepLocal, second.Resolution.Action);
        Assert.HasCount(0, center.Query(conflict.InstanceId));
    }

    [TestMethod]
    public void CoordinatorRestoresResolutionSnapshot()
    {
        var conflict = Create();
        var result = MirrorPulseConflictKeepRemote.Apply(conflict, DateTimeOffset.UtcNow);
        var center = new MirrorPulseConflictCenter();
        var coordinator = new MirrorPulseConflictRecoveryCoordinator(center);

        coordinator.Restore([result]);

        Assert.HasCount(1, coordinator.Snapshot());
        Assert.HasCount(1, center.Query(conflict.InstanceId, pendingOnly: false));
    }

    private static MirrorPulseConflictRecord Create() => new(
        Guid.NewGuid(),
        InstanceId.New(),
        "change",
        "docs/file.txt",
        MirrorPulseConflictReason.Content,
        MirrorPulseVersionComparison.Diverged,
        "local",
        "remote",
        DateTimeOffset.UtcNow);
}

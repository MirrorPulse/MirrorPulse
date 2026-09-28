using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictCenterTests
{
    [TestMethod]
    public void CenterQueriesPendingConflictsByInstanceAndDetectionOrder()
    {
        var center = new MirrorPulseConflictCenter();
        var firstInstance = InstanceId.New();
        var secondInstance = InstanceId.New();
        var oldest = Create(firstInstance, DateTimeOffset.UtcNow.AddMinutes(-2));
        var newest = Create(firstInstance, DateTimeOffset.UtcNow);
        var other = Create(secondInstance, DateTimeOffset.UtcNow.AddMinutes(-1));
        var resolved = Create(firstInstance, DateTimeOffset.UtcNow.AddMinutes(-3), MirrorPulseConflictStatus.Resolved);
        center.Upsert(newest);
        center.Upsert(other);
        center.Upsert(oldest);
        center.Upsert(resolved);

        var pending = center.Query(firstInstance);
        var all = center.Query(firstInstance, pendingOnly: false);

        CollectionAssert.AreEqual(new[] { oldest, newest }, pending.ToArray());
        Assert.HasCount(3, all);
    }

    [TestMethod]
    public void CenterUpsertReplacesTheSameConflictIdentity()
    {
        var center = new MirrorPulseConflictCenter();
        var conflict = Create(InstanceId.New(), DateTimeOffset.UtcNow);
        var resolved = new MirrorPulseConflictRecord(
            conflict.ConflictId,
            conflict.InstanceId,
            conflict.ChangeId,
            conflict.RelativePath,
            conflict.Reason,
            conflict.VersionComparison,
            conflict.LocalRevision,
            conflict.RemoteRevision,
            conflict.DetectedAt,
            MirrorPulseConflictStatus.Resolved);

        center.Upsert(conflict);
        center.Upsert(resolved);

        Assert.HasCount(0, center.Query(conflict.InstanceId));
        Assert.HasCount(1, center.Query(conflict.InstanceId, pendingOnly: false));
    }

    private static MirrorPulseConflictRecord Create(
        InstanceId instanceId,
        DateTimeOffset detectedAt,
        MirrorPulseConflictStatus status = MirrorPulseConflictStatus.Pending) => new(
            Guid.NewGuid(),
            instanceId,
            Guid.NewGuid().ToString("D"),
            "docs/file.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local",
            "remote",
            detectedAt,
            status);
}

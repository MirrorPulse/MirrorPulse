using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictRetryTests
{
    [TestMethod]
    public void RetryLeavesConflictPendingForAnotherSyncAttempt()
    {
        var conflict = new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "change",
            "docs/file.txt",
            MirrorPulseConflictReason.StaleRemoteRevision,
            MirrorPulseVersionComparison.Unknown,
            "local",
            "remote",
            DateTimeOffset.UtcNow);

        var result = MirrorPulseConflictRetry.Apply(conflict, DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseConflictAction.Retry, result.Resolution.Action);
        Assert.AreEqual(MirrorPulseConflictStatus.Pending, result.UpdatedConflict.Status);
        Assert.IsTrue(result.UpdatedConflict.IsPending);
    }
}

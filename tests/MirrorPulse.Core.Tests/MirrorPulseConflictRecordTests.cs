using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictRecordTests
{
    [TestMethod]
    public void ConflictRecordRetainsIdentityVersionAndPendingStatus()
    {
        var detectedAt = DateTimeOffset.UtcNow;
        var record = new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "remote-change-1",
            "docs/report.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local-2",
            "remote-2",
            detectedAt);

        Assert.IsTrue(record.IsPending);
        Assert.AreEqual(MirrorPulseConflictReason.Content, record.Reason);
        Assert.AreEqual(MirrorPulseVersionComparison.Diverged, record.VersionComparison);
        Assert.AreEqual(detectedAt, record.DetectedAt);
    }

    [TestMethod]
    public void ConflictRecordRejectsEmptyIdentityAndNullPath()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseConflictRecord(
            Guid.Empty,
            InstanceId.New(),
            "change",
            "file.txt",
            MirrorPulseConflictReason.Unknown,
            MirrorPulseVersionComparison.Unknown,
            null,
            null,
            DateTimeOffset.UtcNow));

        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "change",
            "",
            MirrorPulseConflictReason.Unknown,
            MirrorPulseVersionComparison.Unknown,
            null,
            null,
            DateTimeOffset.UtcNow));
    }
}

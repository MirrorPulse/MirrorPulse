using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictKeepLocalTests
{
    [TestMethod]
    public void KeepLocalResolvesPendingConflictWithoutPreservedPath()
    {
        var conflict = Create();
        var resolutionId = Guid.NewGuid();
        var appliedAt = DateTimeOffset.UtcNow;

        var result = MirrorPulseConflictResolutionPlanner.KeepLocal(conflict, appliedAt, resolutionId);

        Assert.AreEqual(resolutionId, result.Resolution.ResolutionId);
        Assert.AreEqual(MirrorPulseConflictAction.KeepLocal, result.Resolution.Action);
        Assert.IsNull(result.Resolution.PreservedPath);
        Assert.AreEqual(MirrorPulseConflictStatus.Resolved, result.UpdatedConflict.Status);
    }

    [TestMethod]
    public void ActionsRejectAlreadyResolvedConflicts()
    {
        var conflict = Create();
        var resolved = MirrorPulseConflictResolutionPlanner.KeepLocal(conflict, DateTimeOffset.UtcNow).UpdatedConflict;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            MirrorPulseConflictResolutionPlanner.KeepLocal(resolved, DateTimeOffset.UtcNow));
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

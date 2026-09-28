using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictDeleteSideTests
{
    [TestMethod]
    public void DeleteActionsResolveTheConflictAndIdentifyTheDeletedSide()
    {
        var localResult = MirrorPulseConflictDeleteSide.DeleteLocal(Create(), DateTimeOffset.UtcNow);
        var remoteResult = MirrorPulseConflictDeleteSide.DeleteRemote(Create(), DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseConflictAction.DeleteLocal, localResult.Resolution.Action);
        Assert.AreEqual(MirrorPulseConflictAction.DeleteRemote, remoteResult.Resolution.Action);
        Assert.AreEqual(MirrorPulseConflictStatus.Resolved, localResult.UpdatedConflict.Status);
        Assert.AreEqual(MirrorPulseConflictStatus.Resolved, remoteResult.UpdatedConflict.Status);
    }

    private static MirrorPulseConflictRecord Create() => new(
        Guid.NewGuid(),
        InstanceId.New(),
        "change",
        "docs/file.txt",
        MirrorPulseConflictReason.Delete,
        MirrorPulseVersionComparison.Diverged,
        "local",
        "remote",
        DateTimeOffset.UtcNow);
}

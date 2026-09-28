using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictKeepRemoteTests
{
    [TestMethod]
    public void KeepRemoteResolvesTheConflictAndDoesNotCreateASecondPath()
    {
        var conflict = new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "remote-change",
            "docs/file.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local-2",
            "remote-2",
            DateTimeOffset.UtcNow);

        var result = MirrorPulseConflictKeepRemote.Apply(conflict, DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseConflictAction.KeepRemote, result.Resolution.Action);
        Assert.IsNull(result.Resolution.PreservedPath);
        Assert.AreEqual(MirrorPulseConflictStatus.Resolved, result.UpdatedConflict.Status);
    }
}

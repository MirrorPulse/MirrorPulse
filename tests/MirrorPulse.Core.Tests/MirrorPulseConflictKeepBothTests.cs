using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictKeepBothTests
{
    [TestMethod]
    public void KeepBothRequiresAndRetainsAnExplicitPreservedPath()
    {
        var conflict = Create();
        var result = MirrorPulseConflictKeepBoth.Apply(
            conflict,
            "conflicts/remote-copy.txt",
            DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseConflictAction.KeepBoth, result.Resolution.Action);
        Assert.AreEqual("conflicts/remote-copy.txt", result.Resolution.PreservedPath);
        Assert.AreEqual(MirrorPulseConflictStatus.Resolved, result.UpdatedConflict.Status);
    }

    [TestMethod]
    public void KeepBothRejectsAnEmptyPreservedPath()
    {
        Assert.ThrowsExactly<ArgumentException>(() => MirrorPulseConflictKeepBoth.Apply(
            Create(),
            "",
            DateTimeOffset.UtcNow));
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

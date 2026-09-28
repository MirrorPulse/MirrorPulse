using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseVersionComparisonTests
{
    [TestMethod]
    public void ComparatorClassifiesSameLocalAndRemoteRevision()
    {
        var result = MirrorPulseVersionComparator.Compare("r2", "r1", "r2", "r1");

        Assert.AreEqual(MirrorPulseVersionComparison.Same, result);
    }

    [TestMethod]
    public void ComparatorDistinguishesRemoteLocalAndDivergedChanges()
    {
        Assert.AreEqual(
            MirrorPulseVersionComparison.RemoteAdvanced,
            MirrorPulseVersionComparator.Compare("r1", "r1", "r2", "r1"));
        Assert.AreEqual(
            MirrorPulseVersionComparison.LocalAdvanced,
            MirrorPulseVersionComparator.Compare("local-2", "r1", "r1", "r1"));
        Assert.AreEqual(
            MirrorPulseVersionComparison.Diverged,
            MirrorPulseVersionComparator.Compare("local-2", "r1", "r2", "r1"));
    }

    [TestMethod]
    public void ComparatorUsesUnknownWhenAncestryIsNotProven()
    {
        Assert.AreEqual(
            MirrorPulseVersionComparison.Unknown,
            MirrorPulseVersionComparator.Compare("local", "r1", "r2", "other"));
        Assert.AreEqual(
            MirrorPulseVersionComparison.Unknown,
            MirrorPulseVersionComparator.Compare(null, null, "r2", null));
    }
}

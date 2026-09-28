using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryEchoSuppressorTests
{
    [TestMethod]
    public void SuppressorConsumesOnlyTheRegisteredObservationBudget()
    {
        var suppressor = new MirrorPulseLocalDirectoryEchoSuppressor();
        var now = DateTimeOffset.UtcNow;
        suppressor.Register("nested\\file.txt", MirrorPulseLocalDirectoryChangeKind.Changed, now.AddMinutes(1), 2);

        Assert.IsTrue(suppressor.Observe("nested/file.txt", MirrorPulseLocalDirectoryChangeKind.Changed, now));
        Assert.IsTrue(suppressor.Observe("nested/file.txt", MirrorPulseLocalDirectoryChangeKind.Changed, now));
        Assert.IsFalse(suppressor.Observe("nested/file.txt", MirrorPulseLocalDirectoryChangeKind.Changed, now));
    }

    [TestMethod]
    public void SuppressorRejectsExpiredTokensAndDifferentChangeKinds()
    {
        var suppressor = new MirrorPulseLocalDirectoryEchoSuppressor();
        var now = DateTimeOffset.UtcNow;
        suppressor.Register("file.txt", MirrorPulseLocalDirectoryChangeKind.Created, now.AddSeconds(1), 1);

        Assert.IsFalse(suppressor.Observe("file.txt", MirrorPulseLocalDirectoryChangeKind.Changed, now));
        Assert.IsFalse(suppressor.Observe("file.txt", MirrorPulseLocalDirectoryChangeKind.Created, now.AddSeconds(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => suppressor.Register(
            "file.txt",
            MirrorPulseLocalDirectoryChangeKind.Created,
            now,
            1));
    }
}

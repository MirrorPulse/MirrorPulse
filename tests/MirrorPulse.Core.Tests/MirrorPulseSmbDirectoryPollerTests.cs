using MirrorPulse.Core.Adapters.Smb;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSmbDirectoryPollerTests
{
    [TestMethod]
    public void PollerSortsSmbEntriesAndDetectsMetadataChanges()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new FakeSource([
            new("z.txt", false, 1, now),
            new("a.txt", false, 2, now),
        ]);
        var poller = new MirrorPulseSmbDirectoryPoller("\\\\server\\share", source);

        var current = poller.Snapshot();
        var previous = new[]
        {
            new MirrorPulseSmbEntry("a.txt", false, 1, now),
            new MirrorPulseSmbEntry("removed.txt", false, 5, now),
        };
        var changes = MirrorPulseSmbDirectoryPoller.DetectChanges(previous, current);

        Assert.AreEqual("a.txt", current[0].RelativePath);
        Assert.AreEqual(MirrorPulseSmbChangeKind.Modified, changes[0].Kind);
        Assert.AreEqual(MirrorPulseSmbChangeKind.Removed, changes[1].Kind);
        Assert.AreEqual(MirrorPulseSmbChangeKind.Added, changes[2].Kind);
    }

    [TestMethod]
    public void PollerRejectsDuplicateEntries()
    {
        var now = DateTimeOffset.UtcNow;
        var source = new FakeSource([
            new("File.txt", false, 1, now),
            new("file.txt", false, 1, now),
        ]);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            new MirrorPulseSmbDirectoryPoller("\\\\server\\share", source).Snapshot());
    }

    private sealed class FakeSource(IReadOnlyList<MirrorPulseSmbEntry> entries) : IMirrorPulseSmbEntrySource
    {
        public IEnumerable<MirrorPulseSmbEntry> Enumerate(string networkPath) => entries;
    }
}

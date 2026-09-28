using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictFileNameTests
{
    [TestMethod]
    public void FileNameIsDeterministicAndIncludesConflictIdentity()
    {
        var conflictId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var conflict = Create(conflictId, "docs/report.txt");

        var first = MirrorPulseConflictFileName.Create(conflict);
        var second = MirrorPulseConflictFileName.Create(conflict);

        Assert.AreEqual(first, second);
        StringAssert.Contains(first, conflictId.ToString("N"));
        StringAssert.Contains(first, "report.txt");
        Assert.DoesNotContain(Path.DirectorySeparatorChar.ToString(), first);
    }

    [TestMethod]
    public void FileNameSanitizesSourceCharactersAndSeparatesConflictIds()
    {
        var first = MirrorPulseConflictFileName.Create(Create(Guid.NewGuid(), "docs/bad:name?.txt"));
        var second = MirrorPulseConflictFileName.Create(Create(Guid.NewGuid(), "docs/bad:name?.txt"));

        Assert.DoesNotContain(":", first);
        Assert.DoesNotContain("?", first);
        Assert.AreNotEqual(first, second);
    }

    private static MirrorPulseConflictRecord Create(Guid conflictId, string relativePath) => new(
        conflictId,
        InstanceId.New(),
        "change-1",
        relativePath,
        MirrorPulseConflictReason.Content,
        MirrorPulseVersionComparison.Diverged,
        "local",
        "remote",
        new DateTimeOffset(2026, 9, 28, 8, 0, 0, TimeSpan.Zero));
}

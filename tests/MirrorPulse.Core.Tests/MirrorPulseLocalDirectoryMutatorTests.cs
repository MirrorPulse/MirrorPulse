using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryMutatorTests
{
    [TestMethod]
    public async Task MutatorMovesAndDeletesFilesBelowTheSourceRoot()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-mutator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "old.txt"), "value");
            var mutator = new MirrorPulseLocalDirectoryMutator(source);

            var moved = await mutator.MoveAsync("old.txt", "nested/new.txt");
            var deleted = await mutator.DeleteAsync("nested/new.txt");

            Assert.AreEqual(MirrorPulseLocalDirectoryMutationKind.Move, moved.Kind);
            Assert.AreEqual("nested/new.txt", moved.RelativePath);
            Assert.AreEqual("old.txt", moved.PreviousRelativePath);
            Assert.IsTrue(deleted.Changed);
            Assert.IsFalse(File.Exists(Path.Combine(source, "nested", "new.txt")));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public async Task MutatorTreatsMissingDeletesAsNoOpsAndRejectsExistingDestinations()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-mutator-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        try
        {
            var mutator = new MirrorPulseLocalDirectoryMutator(source);
            var missing = await mutator.DeleteAsync("missing.txt");
            await File.WriteAllTextAsync(Path.Combine(source, "a.txt"), "a");
            await File.WriteAllTextAsync(Path.Combine(source, "b.txt"), "b");

            Assert.IsFalse(missing.Changed);
            await Assert.ThrowsExactlyAsync<IOException>(() => mutator.MoveAsync("a.txt", "b.txt"));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }
}

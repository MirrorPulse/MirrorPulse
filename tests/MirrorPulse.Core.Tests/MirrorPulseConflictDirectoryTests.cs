using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictDirectoryTests
{
    [TestMethod]
    public void EnsureExistsCreatesInstanceDirectoryOutsideSyncRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-conflict-{Guid.NewGuid():N}");
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var instance = InstanceId.New();

        try
        {
            var directory = MirrorPulseConflictDirectory.EnsureExists(paths, instance);

            Assert.IsTrue(Directory.Exists(directory));
            Assert.IsTrue(directory.StartsWith(paths.DataRootPath, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(SourceDirectoryPathNormalizer.IsWithin(paths.SyncRootPath, directory));
            Assert.AreEqual(
                MirrorPulseConflictDirectory.GetPath(paths, instance),
                directory);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

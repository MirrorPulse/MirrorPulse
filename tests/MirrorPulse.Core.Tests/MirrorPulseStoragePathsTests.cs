using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseStoragePathsTests
{
    [TestMethod]
    public void StoragePathsAcceptDistinctRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), "mirrorpulse-storage");
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));

        Assert.AreEqual(Path.Combine(root, "sync"), paths.SyncRootPath);
        Assert.AreEqual(Path.Combine(root, "data"), paths.DataRootPath);
        Assert.AreEqual(Path.Combine(root, "data", "state", "cfsharp.db"), paths.CfSharpStateDatabasePath);
        Assert.IsFalse(SourceDirectoryPathNormalizer.IsWithin(paths.SyncRootPath, paths.CfSharpStateDatabasePath));
    }

    [TestMethod]
    public void StoragePathsRejectEqualAndNestedRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), "mirrorpulse-storage");

        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseStoragePaths(root, root));
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseStoragePaths(root, Path.Combine(root, "data")));
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseStoragePaths(Path.Combine(root, "sync"), root));
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseStoragePaths("relative-sync", Path.Combine(root, "data")));
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseStoragePaths(Path.Combine(root, "sync"), "relative-data"));
    }
}

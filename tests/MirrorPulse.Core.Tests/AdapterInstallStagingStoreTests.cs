using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstallStagingStoreTests
{
    [TestMethod]
    public async Task StageCopiesPackageIntoAnIsolatedDirectoryAndCanBeRemoved()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "source.mpadapter");
            await File.WriteAllTextAsync(source, "package");
            var store = new AdapterInstallStagingStore(Path.Combine(root, "staging"));
            var stage = await store.StageAsync(InstallId.New(), source);

            Assert.IsTrue(File.Exists(stage.PackagePath));
            Assert.AreEqual("package", await File.ReadAllTextAsync(stage.PackagePath));
            store.Remove(stage);
            Assert.IsFalse(Directory.Exists(stage.DirectoryPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task StageRejectsNonMpadapterFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-staging-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "source.zip");
            await File.WriteAllTextAsync(source, "package");
            var store = new AdapterInstallStagingStore(Path.Combine(root, "staging"));

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.StageAsync(InstallId.New(), source));
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

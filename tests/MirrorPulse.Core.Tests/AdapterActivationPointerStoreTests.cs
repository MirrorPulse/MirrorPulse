using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterActivationPointerStoreTests
{
    [TestMethod]
    public async Task ActivePointerSelectsOneVersionWhileOtherVersionsRemainInstalled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-active-{Guid.NewGuid():N}");
        try
        {
            var paths = new CurrentUserAdapterPathProvider(root);
            var adapterId = AdapterId.Parse("example.webdav");
            var firstInstall = InstallId.New();
            var secondInstall = InstallId.New();
            Directory.CreateDirectory(paths.GetInstallationDirectory(adapterId, "1.0.0", firstInstall));
            Directory.CreateDirectory(paths.GetInstallationDirectory(adapterId, "2.0.0", secondInstall));
            var store = new AdapterActivationPointerStore(paths);

            var active = await store.ActivateAsync(adapterId, "2.0.0", secondInstall);
            var read = await store.ReadAsync(adapterId);

            Assert.AreEqual(active, read);
            Assert.AreEqual(secondInstall, read!.InstallId);
            Assert.IsTrue(Directory.Exists(paths.GetInstallationDirectory(adapterId, "1.0.0", firstInstall)));
            Assert.IsTrue(File.Exists(store.GetPointerPath(adapterId)));
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
    public async Task ActivationRejectsAnInstallationThatDoesNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-active-{Guid.NewGuid():N}");
        try
        {
            var store = new AdapterActivationPointerStore(new CurrentUserAdapterPathProvider(root));

            await Assert.ThrowsExactlyAsync<DirectoryNotFoundException>(() =>
                store.ActivateAsync(AdapterId.Parse("example.webdav"), "1.0.0", InstallId.New()));
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

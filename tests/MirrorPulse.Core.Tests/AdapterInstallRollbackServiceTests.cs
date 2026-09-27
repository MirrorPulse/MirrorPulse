using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstallRollbackServiceTests
{
    [TestMethod]
    public async Task RollbackRemovesFailedVersionAndRestoresPreviousActiveVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-rollback-{Guid.NewGuid():N}");
        try
        {
            var paths = new CurrentUserAdapterPathProvider(root);
            var activation = new AdapterActivationPointerStore(paths);
            var rollback = new AdapterInstallRollbackService(paths, activation);
            var adapterId = AdapterId.Parse("example.webdav");
            var previousId = InstallId.New();
            var failedId = InstallId.New();
            var previousDirectory = paths.GetInstallationDirectory(adapterId, "1.0.0", previousId);
            var failedDirectory = paths.GetInstallationDirectory(adapterId, "2.0.0", failedId);
            Directory.CreateDirectory(previousDirectory);
            Directory.CreateDirectory(failedDirectory);
            var previous = await activation.ActivateAsync(adapterId, "1.0.0", previousId);

            var result = await rollback.RollbackAsync(adapterId, "2.0.0", failedId, previous);
            var active = await activation.ReadAsync(adapterId);

            Assert.IsTrue(result.FailedInstallationRemoved);
            Assert.IsTrue(result.PreviousInstallationRestored);
            Assert.IsFalse(Directory.Exists(failedDirectory));
            Assert.AreEqual(previousId, active!.InstallId);
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
    public async Task RollbackWithoutPreviousRemovesTheActivePointer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-rollback-{Guid.NewGuid():N}");
        try
        {
            var paths = new CurrentUserAdapterPathProvider(root);
            var activation = new AdapterActivationPointerStore(paths);
            var rollback = new AdapterInstallRollbackService(paths, activation);
            var adapterId = AdapterId.Parse("example.webdav");
            var failedId = InstallId.New();
            Directory.CreateDirectory(paths.GetInstallationDirectory(adapterId, "1.0.0", failedId));
            await activation.ActivateAsync(adapterId, "1.0.0", failedId);

            var result = await rollback.RollbackAsync(adapterId, "1.0.0", failedId, previous: null);

            Assert.IsFalse(result.PreviousInstallationRestored);
            Assert.IsNull(await activation.ReadAsync(adapterId));
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

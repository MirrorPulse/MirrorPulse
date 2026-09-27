using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterUninstallServiceTests
{
    [TestMethod]
    public async Task UninstallRemovesOnlyTheSelectedVersionAndItsActivePointer()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-uninstall-{Guid.NewGuid():N}");
        try
        {
            var paths = new CurrentUserAdapterPathProvider(root);
            var activation = new AdapterActivationPointerStore(paths);
            var service = new AdapterUninstallService(paths, activation);
            var adapterId = AdapterId.Parse("example.webdav");
            var installId = InstallId.New();
            var otherId = InstallId.New();
            var directory = paths.GetInstallationDirectory(adapterId, "1.0.0", installId);
            var otherDirectory = paths.GetInstallationDirectory(adapterId, "2.0.0", otherId);
            Directory.CreateDirectory(directory);
            Directory.CreateDirectory(otherDirectory);
            await activation.ActivateAsync(adapterId, "1.0.0", installId);
            var installed = CreateInstalledAdapter(adapterId, installId, directory);

            var result = await service.UninstallAsync(installed);

            Assert.IsTrue(result.InstallationRemoved);
            Assert.IsTrue(result.ActivePointerCleared);
            Assert.IsFalse(Directory.Exists(directory));
            Assert.IsTrue(Directory.Exists(otherDirectory));
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

    [TestMethod]
    public async Task UninstallRejectsAPathOutsideTheManagedLayout()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-uninstall-{Guid.NewGuid():N}");
        try
        {
            var paths = new CurrentUserAdapterPathProvider(root);
            var service = new AdapterUninstallService(paths, new AdapterActivationPointerStore(paths));
            var adapterId = AdapterId.Parse("example.webdav");
            var installed = CreateInstalledAdapter(adapterId, InstallId.New(), Path.Combine(root, "outside"));

            await Assert.ThrowsExactlyAsync<ArgumentException>(() => service.UninstallAsync(installed));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static InstalledAdapter CreateInstalledAdapter(AdapterId adapterId, InstallId installId, string directory) => new(
        new AdapterManifest(
            1,
            adapterId,
            "Example Publisher",
            "1.0.0",
            new ProtocolVersionRange(1, 1),
            new Dictionary<string, string>
            {
                ["win-x64"] = "payload/win-x64/Adapter.exe",
                ["win-arm64"] = "payload/win-arm64/Adapter.exe"
            },
            new AdapterInstallPolicy(null),
            new AdapterInstancePolicy(null, null),
            new AdapterCapabilities(true, true, true, true),
            ["en-US"],
            "1.0.0"),
        installId,
        directory,
        Sha256Digest.Parse(new string('B', 64)),
        AdapterInstallSource.LocalFile,
        null,
        isSigned: true,
        DateTimeOffset.UtcNow,
        AdapterLifecycleState.Installed);
}

using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConfigurationStoreTests
{
    [TestMethod]
    public async Task StoreRoundTripsConfigurationAndUsesAtomicReplacement()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-config-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(root, "config.json");
            var store = new MirrorPulseConfigurationStore(path);
            var installation = InstallId.New();
            var configuration = new MirrorPulseConfiguration(
                MirrorPulseConfiguration.CurrentSchemaVersion,
                "zh-CN",
                true,
                false,
                new[] { installation });

            await store.SaveAsync(configuration);
            var loaded = await store.LoadAsync();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(configuration.Locale, loaded.Locale);
            Assert.AreEqual(configuration.DeveloperMode, loaded.DeveloperMode);
            Assert.AreEqual(installation, loaded.EnabledInstallations.Single());
            Assert.IsFalse(Directory.EnumerateFiles(root, "*.tmp").Any());
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
    public async Task StoreReturnsNullForMissingFileAndRejectsCorruptDocument()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-config-{Guid.NewGuid():N}");
        try
        {
            var path = Path.Combine(root, "config.json");
            var store = new MirrorPulseConfigurationStore(path);

            Assert.IsNull(await store.LoadAsync());
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":999}");

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => store.LoadAsync());
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

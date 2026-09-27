using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ConfigurationV0ToV1MigrationTests
{
    [TestMethod]
    public async Task StoreLoadsVersionZeroWithTheV1SyncRootDefault()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-config-migration-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "config.json");
            await File.WriteAllTextAsync(path, "{\"schemaVersion\":0,\"locale\":\"en-US\",\"developerMode\":false,\"startWithWindows\":false,\"enabledInstallations\":[]}");

            var loaded = await new MirrorPulseConfigurationStore(path).LoadAsync();

            Assert.IsNotNull(loaded);
            Assert.AreEqual(MirrorPulseConfiguration.CurrentSchemaVersion, loaded.SchemaVersion);
            Assert.AreEqual("MirrorPulse", loaded.SyncRootDisplayName);
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

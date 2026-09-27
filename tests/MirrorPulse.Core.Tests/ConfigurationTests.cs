using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public void ConfigurationStoresGlobalModeAndEnabledInstallations()
    {
        var installId = InstallId.New();
        var configuration = new MirrorPulseConfiguration(
            MirrorPulseConfiguration.CurrentSchemaVersion,
            "en-US",
            developerMode: true,
            startWithWindows: false,
            enabledInstallations: [installId]);

        Assert.AreEqual(1, configuration.SchemaVersion);
        Assert.IsTrue(configuration.DeveloperMode);
        Assert.IsFalse(configuration.StartWithWindows);
        Assert.AreEqual("MirrorPulse", configuration.SyncRootDisplayName);
        CollectionAssert.Contains(configuration.EnabledInstallations.ToArray(), installId);
    }

    [TestMethod]
    public void ConfigurationCopiesAndDeduplicatesEnabledInstallations()
    {
        var installId = InstallId.New();
        var installations = new List<InstallId> { installId };
        var configuration = new MirrorPulseConfiguration(1, "zh-CN", false, true, installations);
        installations.Add(InstallId.New());

        Assert.HasCount(1, configuration.EnabledInstallations);
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseConfiguration(1, "en-US", false, false, [installId, installId]));
    }
}

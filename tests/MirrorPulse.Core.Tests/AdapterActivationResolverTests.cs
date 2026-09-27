using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterActivationResolverTests
{
    [TestMethod]
    public void ResolverKeepsDisabledInstallationsOffline()
    {
        var enabled = InstallId.New();
        var disabled = InstallId.New();
        var configuration = new MirrorPulseConfiguration(
            MirrorPulseConfiguration.CurrentSchemaVersion,
            "en-US",
            false,
            false,
            new[] { enabled });

        var snapshot = AdapterActivationResolver.Resolve(configuration, new[] { enabled, disabled });

        Assert.IsTrue(snapshot.IsEnabled(enabled));
        Assert.IsFalse(snapshot.IsEnabled(disabled));
        Assert.AreEqual(disabled, snapshot.OfflineDisabled.Single());
        Assert.HasCount(0, snapshot.Missing);
    }

    [TestMethod]
    public void ResolverReportsEnabledButUninstalledIds()
    {
        var missing = InstallId.New();
        var configuration = new MirrorPulseConfiguration(
            MirrorPulseConfiguration.CurrentSchemaVersion,
            "en-US",
            false,
            false,
            new[] { missing });

        var snapshot = AdapterActivationResolver.Resolve(configuration, Array.Empty<InstallId>());

        Assert.HasCount(0, snapshot.Enabled);
        Assert.AreEqual(missing, snapshot.Missing.Single());
    }
}

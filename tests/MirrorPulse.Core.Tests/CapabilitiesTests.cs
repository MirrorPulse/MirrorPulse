using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CapabilitiesTests
{
    [TestMethod]
    public void CapabilityDeclarationPreservesManifestValues()
    {
        var capabilities = new AdapterCapabilities(
            network: true,
            sourceDirectory: true,
            remoteChanges: false,
            rangeRead: true);

        Assert.IsTrue(capabilities.Network);
        Assert.IsTrue(capabilities.SourceDirectory);
        Assert.IsFalse(capabilities.RemoteChanges);
        Assert.IsTrue(capabilities.RangeRead);
    }

    [TestMethod]
    public void CapabilityDeclarationCanRepresentOfflineLocalStorage()
    {
        var capabilities = new AdapterCapabilities(
            network: false,
            sourceDirectory: true,
            remoteChanges: false,
            rangeRead: false);

        Assert.IsFalse(capabilities.Network);
        Assert.IsTrue(capabilities.SourceDirectory);
    }
}

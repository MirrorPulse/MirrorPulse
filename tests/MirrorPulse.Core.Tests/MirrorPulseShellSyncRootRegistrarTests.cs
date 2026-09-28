using System.Runtime.Versioning;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseShellSyncRootRegistrarTests
{
    [TestMethod]
    public void ProfileUsesOneStableCurrentUserRootIdentity()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", "shell-root");
        var providerId = Guid.Parse("89f1747b-62aa-48bd-a725-e33f20c271a5");
        var definition = new MirrorPulseSyncRootDefinition(rootPath, "0.1.0", providerId, [1, 2, 3]);

        var first = MirrorPulseShellSyncRootRegistrar.CreateProfile(definition, "S-1-5-21-123");
        var second = MirrorPulseShellSyncRootRegistrar.CreateProfile(definition, "S-1-5-21-123");

        Assert.AreEqual("MirrorPulse!S-1-5-21-123!Default", first.RegistrationId);
        Assert.AreEqual(first.RegistrationId, second.RegistrationId);
        Assert.AreEqual(rootPath, first.SyncRootPath);
        Assert.AreEqual(providerId, first.ProviderId);
        Assert.AreEqual("MirrorPulse", first.DisplayName);
        Assert.AreEqual("0.1.0", first.ProviderVersion);
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.IconResource));
    }
}

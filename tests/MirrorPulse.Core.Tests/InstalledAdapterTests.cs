using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class InstalledAdapterTests
{
    [TestMethod]
    public void RepeatedInstallationProducesIndependentInstallIds()
    {
        var first = CreateInstalledAdapter(InstallId.New());
        var second = CreateInstalledAdapter(InstallId.New());

        Assert.AreEqual(first.AdapterId, second.AdapterId);
        Assert.AreEqual(first.Version, second.Version);
        Assert.AreNotEqual(first.InstallId, second.InstallId);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void InstalledAdapterRetainsTrustAndSourceMetadata()
    {
        var installed = CreateInstalledAdapter(InstallId.New());

        Assert.IsTrue(installed.IsSigned);
        Assert.AreEqual(AdapterInstallSource.OfficialRelease, installed.Source);
        Assert.AreEqual(AdapterLifecycleState.Installed, installed.LifecycleState);
        Assert.AreEqual("Example Publisher", installed.Publisher);
    }

    private static InstalledAdapter CreateInstalledAdapter(InstallId installId) => new(
        CreateManifest(),
        installId,
        $"C:\\MirrorPulse\\adapters\\{installId}",
        Sha256Digest.Parse(new string('C', 64)),
        AdapterInstallSource.OfficialRelease,
        "https://github.com/example/adapter/releases/download/v1.0.0/example.mpadapter",
        isSigned: true,
        new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        AdapterLifecycleState.Installed);

    private static AdapterManifest CreateManifest() => new(
        1,
        AdapterId.Parse("example.webdav"),
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
        "1.0.0");
}

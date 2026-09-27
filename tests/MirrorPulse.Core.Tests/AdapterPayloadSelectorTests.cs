using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterPayloadSelectorTests
{
    [TestMethod]
    public void SelectorChoosesTheRequestedWindowsArchitecture()
    {
        var selection = AdapterPayloadSelector.Select(CreateManifest(), "win-arm64");

        Assert.AreEqual("win-arm64", selection.RuntimeIdentifier);
        Assert.AreEqual("payload/win-arm64/Adapter.exe", selection.Entrypoint);
    }

    [TestMethod]
    public void SelectorRejectsUnsupportedArchitecture()
    {
        Assert.ThrowsExactly<PlatformNotSupportedException>(() =>
            AdapterPayloadSelector.Select(CreateManifest(), "linux-x64"));
    }

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

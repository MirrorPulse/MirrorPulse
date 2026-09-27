using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstallationLimitValidatorTests
{
    [TestMethod]
    public void UnlimitedPolicyAllowsRepeatedIndependentInstallations()
    {
        var result = AdapterInstallationLimitValidator.Validate(CreateManifest(null), 25);

        Assert.IsTrue(result.Allowed);
        Assert.IsNull(result.ErrorCode);
    }

    [TestMethod]
    public void MaximumPolicyRejectsTheNextInstallationAtCapacity()
    {
        var result = AdapterInstallationLimitValidator.Validate(CreateManifest(2), 2);

        Assert.IsFalse(result.Allowed);
        Assert.AreEqual("install.maxInstallationsReached", result.ErrorCode);
    }

    private static AdapterManifest CreateManifest(int? maximumInstallations) => new(
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
        new AdapterInstallPolicy(maximumInstallations),
        new AdapterInstancePolicy(null, null),
        new AdapterCapabilities(true, true, true, true),
        ["en-US"],
        "1.0.0");
}

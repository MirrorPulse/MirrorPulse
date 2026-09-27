using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PrivacySettingsTests
{
    [TestMethod]
    public void DefaultPrivacySettingsAreLocalOnly()
    {
        var settings = new PrivacySettings();

        Assert.IsFalse(settings.CollectUsageData);
        Assert.IsFalse(settings.AllowDiagnosticUpload);
        Assert.IsTrue(settings.IsLocalOnly);
    }

    [TestMethod]
    public void PrivacySettingsRejectTelemetryAndDiagnosticUpload()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new PrivacySettings(collectUsageData: true));
        Assert.ThrowsExactly<ArgumentException>(() => new PrivacySettings(allowDiagnosticUpload: true));
    }
}

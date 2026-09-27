using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterQuarantineRegistryTests
{
    [TestMethod]
    public void RegistryBlocksAndCountsRepeatedValidationFailures()
    {
        var registry = new AdapterQuarantineRegistry();
        var installId = MirrorPulse.Core.Contracts.InstallId.New();
        var now = DateTimeOffset.UtcNow;

        var first = registry.Quarantine(installId, "signature invalid", now);
        var second = registry.Quarantine(installId, "manifest invalid", now.AddMinutes(1));

        Assert.AreEqual(1, first.FailureCount);
        Assert.AreEqual(2, second.FailureCount);
        Assert.IsTrue(registry.IsQuarantined(installId));
        Assert.AreEqual("manifest invalid", registry.TryGet(installId, out var record) ? record!.Reason : string.Empty);
    }

    [TestMethod]
    public void RegistryAllowsExplicitClear()
    {
        var registry = new AdapterQuarantineRegistry();
        var installId = MirrorPulse.Core.Contracts.InstallId.New();
        registry.Quarantine(installId, "validation failed", DateTimeOffset.UtcNow);

        Assert.IsTrue(registry.Clear(installId));
        Assert.IsFalse(registry.IsQuarantined(installId));
        Assert.IsFalse(registry.Clear(installId));
    }
}

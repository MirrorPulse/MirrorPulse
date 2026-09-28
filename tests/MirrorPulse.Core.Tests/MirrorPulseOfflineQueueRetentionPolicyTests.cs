using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseOfflineQueueRetentionPolicyTests
{
    [TestMethod]
    public void EnabledAdapterMayDrainWhileDisabledAdapterStaysOffline()
    {
        var enabled = InstallId.New();
        var disabled = InstallId.New();
        var activation = new AdapterActivationSnapshot([enabled], [disabled], []);

        var enabledDecision = MirrorPulseOfflineQueueRetentionPolicy.Resolve(activation, enabled);
        var disabledDecision = MirrorPulseOfflineQueueRetentionPolicy.Resolve(activation, disabled);

        Assert.IsTrue(enabledDecision.WorkerMayRun);
        Assert.IsTrue(enabledDecision.DrainPendingOperations);
        Assert.IsTrue(enabledDecision.RetainPendingOperations);
        Assert.IsFalse(disabledDecision.WorkerMayRun);
        Assert.IsFalse(disabledDecision.DrainPendingOperations);
        Assert.IsTrue(disabledDecision.RetainPendingOperations);
    }

    [TestMethod]
    public void MissingAdapterInstallationRemainsRetainedAndOffline()
    {
        var missing = InstallId.New();
        var disposition = MirrorPulseOfflineQueueRetentionPolicy.Resolve(
            new AdapterActivationSnapshot([], [], [missing]),
            missing);

        Assert.IsFalse(disposition.WorkerMayRun);
        Assert.IsTrue(disposition.RetainPendingOperations);
        Assert.IsFalse(disposition.DrainPendingOperations);
    }
}

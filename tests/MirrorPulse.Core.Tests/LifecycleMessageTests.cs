using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LifecycleMessageTests
{
    [TestMethod]
    public void CancelTargetsOneRequestAndShutdownCarriesGracePeriod()
    {
        var instanceId = InstanceId.New();
        var sessionId = WorkerSessionId.New();
        var targetRequestId = Guid.NewGuid();
        var cancel = new CancelMessage(
            Guid.NewGuid(),
            instanceId,
            sessionId,
            targetRequestId,
            CancelReason.Timeout,
            force: false);
        var shutdown = new ShutdownMessage(
            Guid.NewGuid(),
            instanceId,
            sessionId,
            ShutdownReason.Update,
            TimeSpan.FromSeconds(5),
            restartAfterExit: true);

        Assert.AreEqual(targetRequestId, cancel.TargetRequestId);
        Assert.AreEqual(CancelReason.Timeout, cancel.Reason);
        Assert.AreEqual(TimeSpan.FromSeconds(5), shutdown.GracePeriod);
        Assert.IsTrue(shutdown.RestartAfterExit);
    }

    [TestMethod]
    public void LifecycleMessagesRejectInvalidTargetsAndGracePeriods()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CancelMessage(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            Guid.Empty,
            CancelReason.UserRequested,
            force: false));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ShutdownMessage(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            ShutdownReason.Fault,
            TimeSpan.FromSeconds(-1),
            restartAfterExit: false));
    }
}

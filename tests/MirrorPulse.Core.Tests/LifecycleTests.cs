using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LifecycleTests
{
    [TestMethod]
    public void HostLifecycleIncludesOperationalAndFailureStates()
    {
        Assert.IsTrue(Enum.IsDefined(MirrorPulseLifecycleState.Running));
        Assert.IsTrue(Enum.IsDefined(MirrorPulseLifecycleState.Degraded));
        Assert.IsTrue(Enum.IsDefined(MirrorPulseLifecycleState.Failed));
    }

    [TestMethod]
    public void AdapterLifecycleIncludesVerificationAndQuarantineStates()
    {
        Assert.IsTrue(Enum.IsDefined(AdapterLifecycleState.Verified));
        Assert.IsTrue(Enum.IsDefined(AdapterLifecycleState.Healthy));
        Assert.IsTrue(Enum.IsDefined(AdapterLifecycleState.Quarantined));
        Assert.IsTrue(Enum.IsDefined(AdapterLifecycleState.RolledBack));
    }

    [TestMethod]
    public void WorkerLifecycleIncludesHandshakeAndStopStates()
    {
        Assert.IsTrue(Enum.IsDefined(WorkerLifecycleState.Handshaking));
        Assert.IsTrue(Enum.IsDefined(WorkerLifecycleState.Healthy));
        Assert.IsTrue(Enum.IsDefined(WorkerLifecycleState.Stopped));
        Assert.IsTrue(Enum.IsDefined(WorkerLifecycleState.Failed));
    }
}

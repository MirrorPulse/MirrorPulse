using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class HostShutdownCoordinatorTests
{
    [TestMethod]
    public async Task CoordinatorStopsRuntimesAndReachesStoppedState()
    {
        var first = InstallId.New();
        var second = InstallId.New();
        var stopper = new FakeStopper();
        var coordinator = new HostShutdownCoordinator(stopper);

        var result = await coordinator.StopAsync(new[] { first, second, first });

        Assert.AreEqual(MirrorPulseLifecycleState.Stopped, result.State);
        Assert.HasCount(2, result.StoppedInstallations);
        Assert.HasCount(2, stopper.Stopped);
    }

    [TestMethod]
    public async Task CoordinatorReportsFailedShutdownWithoutHidingOtherFailures()
    {
        var failed = InstallId.New();
        var healthy = InstallId.New();
        var coordinator = new HostShutdownCoordinator(new FakeStopper(failed));

        var result = await coordinator.StopAsync(new[] { failed, healthy });

        Assert.AreEqual(MirrorPulseLifecycleState.Failed, result.State);
        Assert.HasCount(1, result.StoppedInstallations);
        Assert.HasCount(1, result.Failures);
    }

    private sealed class FakeStopper(InstallId? failed = null) : IAdapterRuntimeStopper
    {
        public List<InstallId> Stopped { get; } = [];

        public ValueTask StopAsync(InstallId installId, CancellationToken cancellationToken = default)
        {
            if (failed == installId)
            {
                throw new InvalidOperationException("stop failed");
            }

            Stopped.Add(installId);
            return ValueTask.CompletedTask;
        }
    }
}

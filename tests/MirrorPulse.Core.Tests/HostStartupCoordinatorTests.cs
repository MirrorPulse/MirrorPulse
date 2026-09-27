using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class HostStartupCoordinatorTests
{
    [TestMethod]
    public async Task CoordinatorStartsAllInstallationsAndReportsRunning()
    {
        var first = InstallId.New();
        var second = InstallId.New();
        var starter = new FakeStarter();
        var coordinator = new HostStartupCoordinator(starter);

        var result = await coordinator.StartAsync(new[] { first, second, first });

        Assert.AreEqual(MirrorPulseLifecycleState.Running, result.State);
        Assert.HasCount(2, result.StartedInstallations);
        Assert.HasCount(0, result.Failures);
        Assert.HasCount(2, starter.Started);
    }

    [TestMethod]
    public async Task CoordinatorContinuesAfterAdapterFailure()
    {
        var failed = InstallId.New();
        var healthy = InstallId.New();
        var starter = new FakeStarter(failed);
        var coordinator = new HostStartupCoordinator(starter);

        var result = await coordinator.StartAsync(new[] { failed, healthy });

        Assert.AreEqual(MirrorPulseLifecycleState.Degraded, result.State);
        Assert.HasCount(1, result.StartedInstallations);
        Assert.HasCount(1, result.Failures);
        Assert.IsTrue(result.Failures.ContainsKey(failed));
    }

    private sealed class FakeStarter(InstallId? failed = null) : IAdapterRuntimeStarter
    {
        public List<InstallId> Started { get; } = [];

        public ValueTask StartAsync(InstallId installId, CancellationToken cancellationToken = default)
        {
            if (failed == installId)
            {
                throw new InvalidOperationException("start failed");
            }

            Started.Add(installId);
            return ValueTask.CompletedTask;
        }
    }
}

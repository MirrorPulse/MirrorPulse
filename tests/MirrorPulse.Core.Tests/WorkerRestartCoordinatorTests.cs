using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerRestartCoordinatorTests
{
    [TestMethod]
    public async Task CoordinatorLimitsCrashRestartsWithinRollingWindow()
    {
        var installId = InstallId.New();
        var starter = new FakeStarter();
        var coordinator = new WorkerRestartCoordinator(starter, new WorkerRestartPolicy(2, TimeSpan.FromMinutes(1), TimeSpan.Zero));
        var now = DateTimeOffset.UtcNow;

        var first = await coordinator.TryRestartAsync(installId, now);
        var second = await coordinator.TryRestartAsync(installId, now.AddSeconds(1));
        var blocked = await coordinator.TryRestartAsync(installId, now.AddSeconds(2));
        var afterWindow = await coordinator.TryRestartAsync(installId, now.AddMinutes(1));

        Assert.IsTrue(first.Started);
        Assert.AreEqual(1, first.Attempt);
        Assert.IsTrue(second.Started);
        Assert.IsFalse(blocked.Started);
        Assert.IsNotNull(blocked.RetryAfter);
        Assert.IsTrue(afterWindow.Started);
        Assert.HasCount(3, starter.Started);
    }

    [TestMethod]
    public void PolicyRejectsInvalidLimits()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkerRestartPolicy(0, TimeSpan.FromMinutes(1), TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkerRestartPolicy(1, TimeSpan.Zero, TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkerRestartPolicy(1, TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(-1)));
    }

    private sealed class FakeStarter : IWorkerRestartStarter
    {
        public List<InstallId> Started { get; } = [];

        public ValueTask StartAsync(InstallId installId, CancellationToken cancellationToken = default)
        {
            Started.Add(installId);
            return ValueTask.CompletedTask;
        }
    }
}

using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerHealthAggregatorTests
{
    [TestMethod]
    public void AggregatorKeepsNewestHealthAndComputesOverallStatus()
    {
        var aggregator = new WorkerHealthAggregator();
        var instanceId = InstanceId.New();
        var session = WorkerSessionId.New();
        var newer = new HealthMessage(Guid.NewGuid(), instanceId, session, HealthStatus.Healthy, 2, DateTimeOffset.UtcNow);
        var older = new HealthMessage(Guid.NewGuid(), instanceId, session, HealthStatus.Unhealthy, 1, DateTimeOffset.UtcNow);

        Assert.IsTrue(aggregator.Observe(newer));
        Assert.IsFalse(aggregator.Observe(older));
        Assert.AreEqual(HealthStatus.Healthy, aggregator.OverallStatus);
        Assert.AreEqual(2, aggregator.Snapshot()[instanceId].Sequence);
    }

    [TestMethod]
    public void AggregatorMarksDisconnectedWorkerUnhealthy()
    {
        var aggregator = new WorkerHealthAggregator();
        var instanceId = InstanceId.New();
        var session = WorkerSessionId.New();

        aggregator.MarkDisconnected(instanceId, session, DateTimeOffset.UtcNow);

        Assert.AreEqual(HealthStatus.Unhealthy, aggregator.OverallStatus);
        Assert.AreEqual(WorkerLifecycleState.Failed, aggregator.Snapshot()[instanceId].LifecycleState);
    }
}

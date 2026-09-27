using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class HealthMessageTests
{
    [TestMethod]
    public void HealthResponseCorrelatesWithHeartbeatSequence()
    {
        var instanceId = InstanceId.New();
        var sessionId = WorkerSessionId.New();
        var sentAt = DateTimeOffset.UtcNow;
        var heartbeat = new HeartbeatMessage(Guid.NewGuid(), instanceId, sessionId, 4, sentAt);
        var health = new HealthMessage(
            heartbeat.RequestId,
            instanceId,
            sessionId,
            HealthStatus.Healthy,
            heartbeat.Sequence,
            sentAt.AddMilliseconds(10));

        Assert.AreEqual("Heartbeat", heartbeat.MessageType);
        Assert.AreEqual("Health", health.MessageType);
        Assert.AreEqual(heartbeat.Sequence, health.Sequence);
        Assert.AreEqual(HealthStatus.Healthy, health.Status);
    }

    [TestMethod]
    public void HeartbeatRejectsNegativeSequence()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HeartbeatMessage(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            -1,
            DateTimeOffset.UtcNow));
    }
}

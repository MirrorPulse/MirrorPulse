using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PersistentStateTests
{
    [TestMethod]
    public void PersistentStateStoresCheckpointsAndQueueCounters()
    {
        var instanceId = InstanceId.New();
        var checkpoint = new InstancePersistentState("local-7", "remote-11", 7, 2, 1, null);
        var state = new MirrorPulsePersistentState(
            MirrorPulsePersistentState.CurrentSchemaVersion,
            new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            new Dictionary<InstanceId, InstancePersistentState> { [instanceId] = checkpoint });

        Assert.AreEqual(1, state.SchemaVersion);
        Assert.AreEqual("remote-11", state.Instances[instanceId].RemoteChangeCursor);
        Assert.AreEqual(2, state.Instances[instanceId].PendingOperationCount);
    }

    [TestMethod]
    public void PersistentStateCopiesInstanceMapAndRejectsNegativeCounters()
    {
        var instanceId = InstanceId.New();
        var instances = new Dictionary<InstanceId, InstancePersistentState>
        {
            [instanceId] = new InstancePersistentState(null, null, 0, 0, 0, null)
        };
        var state = new MirrorPulsePersistentState(1, DateTimeOffset.UtcNow, instances);
        instances[InstanceId.New()] = new InstancePersistentState(null, null, 0, 0, 0, null);

        Assert.HasCount(1, state.Instances);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new InstancePersistentState(null, null, -1, 0, 0, null));
    }
}

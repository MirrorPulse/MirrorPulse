using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CfSharpStateStoreTests
{
    [TestMethod]
    public async Task WrapperRoundTripsPersistentStateThroughBackend()
    {
        var backend = new InMemoryStateStore();
        var wrapper = new MirrorPulsePersistentStateStore(backend);
        var instanceId = InstanceId.New();
        var state = new MirrorPulsePersistentState(
            MirrorPulsePersistentState.CurrentSchemaVersion,
            DateTimeOffset.UtcNow,
            new Dictionary<InstanceId, InstancePersistentState>
            {
                [instanceId] = new InstancePersistentState("local-4", "remote-9", 4, 2, 1, DateTimeOffset.UtcNow),
            });

        await wrapper.SaveAsync(state);
        var loaded = await wrapper.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(state.SchemaVersion, loaded.SchemaVersion);
        Assert.AreEqual(state.Instances[instanceId].RemoteChangeCursor, loaded.Instances[instanceId].RemoteChangeCursor);
        Assert.AreEqual(MirrorPulsePersistentStateStore.DefaultKey, backend.LastKey);
    }

    [TestMethod]
    public async Task WrapperReturnsNullWhenBackendHasNoDocument()
    {
        var wrapper = new MirrorPulsePersistentStateStore(new InMemoryStateStore());

        Assert.IsNull(await wrapper.LoadAsync());
    }

    private sealed class InMemoryStateStore : ICfSharpStateStore
    {
        private ReadOnlyMemory<byte>? _value;

        public string? LastKey { get; private set; }

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            LastKey = key;
            return ValueTask.FromResult(_value);
        }

        public ValueTask WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            LastKey = key;
            _value = value.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}

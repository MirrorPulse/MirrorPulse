using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseRemoteCursorStoreTests
{
    [TestMethod]
    public async Task SaveAndLoadUsesInstanceScopedStateKey()
    {
        var backend = new InMemoryStateStore();
        var store = new MirrorPulseRemoteCursorStore(backend);
        var instance = InstanceId.New();
        var cursor = new byte[] { 3, 1, 4 };

        await store.SaveAsync(instance, cursor);
        var loaded = await store.LoadAsync(instance);

        Assert.IsNotNull(loaded);
        CollectionAssert.AreEqual(cursor, loaded.Value.ToArray());
        Assert.IsTrue(backend.Values.ContainsKey(MirrorPulseRemoteCursorStore.GetKey(instance)));
    }

    [TestMethod]
    public async Task MissingInstanceReturnsNull()
    {
        var store = new MirrorPulseRemoteCursorStore(new InMemoryStateStore());

        Assert.IsNull(await store.LoadAsync(InstanceId.New()));
    }

    private sealed class InMemoryStateStore : ICfSharpStateStore
    {
        public Dictionary<string, byte[]> Values { get; } = new(StringComparer.Ordinal);

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!Values.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            ReadOnlyMemory<byte> copy = value.ToArray();
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(copy);
        }

        public ValueTask WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            Values[key] = value.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}

using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseRestartRecoveryCoordinatorTests
{
    [TestMethod]
    public async Task CheckpointRoundTripsResumeAndRescanDecision()
    {
        var backend = new InMemoryStateStore();
        var coordinator = new MirrorPulseRestartRecoveryCoordinator(backend);
        var instance = InstanceId.New();
        var savedAt = DateTimeOffset.UtcNow;

        await coordinator.SaveAsync(instance, "remote-apply", true, new byte[] { 9, 8 }, savedAt);
        var decision = await coordinator.RestoreAsync(instance);

        Assert.IsNotNull(decision);
        Assert.AreEqual(instance, decision.InstanceId);
        Assert.AreEqual("remote-apply", decision.Phase);
        Assert.IsTrue(decision.RequiresFullRescan);
        CollectionAssert.AreEqual(new byte[] { 9, 8 }, decision.RemoteCursor.ToArray());
        Assert.AreEqual(savedAt, decision.SavedAt);
    }

    [TestMethod]
    public async Task MissingInstanceHasNoRecoveryDecision()
    {
        var coordinator = new MirrorPulseRestartRecoveryCoordinator(new InMemoryStateStore());

        Assert.IsNull(await coordinator.RestoreAsync(InstanceId.New()));
    }

    private sealed class InMemoryStateStore : ICfSharpStateStore
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken = default)
        {
            if (!_values.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            ReadOnlyMemory<byte> copy = value.ToArray();
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(copy);
        }

        public ValueTask WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            _values[key] = value.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}

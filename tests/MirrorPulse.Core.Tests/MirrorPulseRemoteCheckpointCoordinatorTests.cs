using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteCheckpointCoordinatorTests
{
    [TestMethod]
    public async Task CoordinatorSavesCursorAfterAppliedBatch()
    {
        var backend = new InMemoryStateStore();
        var instance = InstanceId.New();
        var expected = new MirrorPulseRemoteBatchOutcome(
            CloudRemoteBatchStatus.Applied,
            new byte[] { 7, 8 },
            false,
            1,
            []);
        var coordinator = CreateCoordinator(expected, backend);

        var actual = await coordinator.ApplyAsync(instance, CreateBatch());
        var saved = await new MirrorPulseRemoteCursorStore(backend).LoadAsync(instance);

        Assert.AreSame(expected, actual);
        Assert.IsNotNull(saved);
        CollectionAssert.AreEqual(new byte[] { 7, 8 }, saved.Value.ToArray());
    }

    [TestMethod]
    public async Task CoordinatorDoesNotSaveCursorForRetryableBatch()
    {
        var backend = new InMemoryStateStore();
        var expected = new MirrorPulseRemoteBatchOutcome(
            CloudRemoteBatchStatus.Applying,
            new byte[] { 7, 8 },
            true,
            0,
            []);
        var coordinator = CreateCoordinator(expected, backend);

        await coordinator.ApplyAsync(InstanceId.New(), CreateBatch());

        Assert.HasCount(0, backend.Values);
    }

    private static MirrorPulseRemoteCheckpointCoordinator CreateCoordinator(
        MirrorPulseRemoteBatchOutcome outcome,
        InMemoryStateStore backend) => new(
            new MirrorPulseRemoteBatchApplier((_, _, _) => ValueTask.FromResult(outcome)),
            new MirrorPulseRemoteCursorStore(backend));

    private static CloudRemoteChangeBatch CreateBatch() => new(
        "batch-checkpoint",
        ReadOnlyMemory<byte>.Empty,
        Array.Empty<CloudRemoteChange>(),
        new byte[] { 1 });

    private sealed class InMemoryStateStore : ICfSharpStateStore
    {
        public Dictionary<string, byte[]> Values { get; } = new(StringComparer.Ordinal);

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(
            string key,
            CancellationToken cancellationToken = default)
        {
            if (!Values.TryGetValue(key, out var value))
            {
                return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
            }

            ReadOnlyMemory<byte> copy = value.ToArray();
            return ValueTask.FromResult<ReadOnlyMemory<byte>?>(copy);
        }

        public ValueTask WriteAsync(
            string key,
            ReadOnlyMemory<byte> value,
            CancellationToken cancellationToken = default)
        {
            Values[key] = value.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}

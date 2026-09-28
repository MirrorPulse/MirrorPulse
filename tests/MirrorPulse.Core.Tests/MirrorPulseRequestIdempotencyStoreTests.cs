using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseRequestIdempotencyStoreTests
{
    [TestMethod]
    public async Task FirstFingerprintIsStoredAndSameRetryIsIgnored()
    {
        var backend = new InMemoryStateStore();
        var store = new MirrorPulseRequestIdempotencyStore(backend);
        var requestId = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow;

        Assert.IsTrue(await store.TryRecordAsync(requestId, "sha256:abc", recordedAt));
        Assert.IsFalse(await store.TryRecordAsync(requestId, "sha256:abc", recordedAt.AddMinutes(1)));
        var loaded = await store.ReadAsync(requestId);

        Assert.IsNotNull(loaded);
        Assert.AreEqual(requestId, loaded.RequestId);
        Assert.AreEqual("sha256:abc", loaded.Fingerprint);
        Assert.AreEqual(recordedAt, loaded.RecordedAt);
    }

    [TestMethod]
    public async Task DifferentFingerprintForSameRequestIsRejected()
    {
        var store = new MirrorPulseRequestIdempotencyStore(new InMemoryStateStore());
        var requestId = Guid.NewGuid();
        await store.TryRecordAsync(requestId, "sha256:abc", DateTimeOffset.UtcNow);

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
            store.TryRecordAsync(requestId, "sha256:def", DateTimeOffset.UtcNow).AsTask());
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

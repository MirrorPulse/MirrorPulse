using System.Globalization;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSyncRootRegistrationStateStoreTests
{
    [TestMethod]
    public async Task StateStoreRoundTripsTheExactRegistrationPath()
    {
        var backend = new InMemoryStateStore();
        var store = new MirrorPulseSyncRootRegistrationStateStore(backend);
        var state = new MirrorPulseSyncRootRegistrationState(
            Path.Combine(Path.GetTempPath(), "MirrorPulse", "MirrorPulse"),
            Guid.Parse("7b6a7d8e-63a5-4b8a-9c6e-1e6e61f95e9d"),
            "0.1.0",
            DateTimeOffset.Parse("2026-09-27T12:00:00Z", CultureInfo.InvariantCulture));

        await store.SaveAsync(state);
        var loaded = await store.LoadAsync();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(Path.GetFullPath(state.Path), loaded.Path);
        Assert.AreEqual(state.ProviderId, loaded.ProviderId);
        Assert.AreEqual(state.ProviderVersion, loaded.ProviderVersion);
        Assert.AreEqual(state.RegisteredAt, loaded.RegisteredAt);
    }

    private sealed class InMemoryStateStore : ICfSharpStateStore
    {
        private ReadOnlyMemory<byte>? _value;

        public ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_value);

        public ValueTask WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default)
        {
            _value = value.ToArray();
            return ValueTask.CompletedTask;
        }
    }
}

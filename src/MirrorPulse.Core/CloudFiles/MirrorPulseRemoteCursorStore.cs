using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Persists the last safely applied remote cursor for each Adapter instance.
/// </summary>
public sealed class MirrorPulseRemoteCursorStore
{
    public const string KeyPrefix = "mirrorpulse/remote-cursor/v1";

    private readonly ICfSharpStateStore _store;

    public MirrorPulseRemoteCursorStore(ICfSharpStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<ReadOnlyMemory<byte>?> LoadAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default)
    {
        var value = await _store
            .ReadAsync(GetKey(instanceId), cancellationToken)
            .ConfigureAwait(false);
        if (value is null)
        {
            return null;
        }

        ReadOnlyMemory<byte> copy = value.Value.ToArray();
        return copy;
    }

    public ValueTask SaveAsync(
        InstanceId instanceId,
        ReadOnlyMemory<byte> cursor,
        CancellationToken cancellationToken = default) =>
        _store.WriteAsync(GetKey(instanceId), cursor.ToArray(), cancellationToken);

    public static string GetKey(InstanceId instanceId) => $"{KeyPrefix}/{instanceId}";
}

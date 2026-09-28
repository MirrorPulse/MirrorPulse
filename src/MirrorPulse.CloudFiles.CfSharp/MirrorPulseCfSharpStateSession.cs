using CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Captures the official store owned by CloudFileSystem so MP may use its public operation
/// repository for retry metadata. This wrapper never owns or disposes the store.
/// </summary>
public sealed class MirrorPulseCfSharpStateSession : ICloudStateStoreFactory
{
    private readonly ICloudStateStoreFactory _inner;
    private ICloudStateStore? _store;

    public MirrorPulseCfSharpStateSession(MirrorPulseStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _inner = MirrorPulseCfSharpStateStoreFactory.Create(paths);
    }

    public ICloudStateStore OpenStore => Volatile.Read(ref _store)
        ?? throw new InvalidOperationException("The CfSharp state store has not opened.");

    public async ValueTask<ICloudStateStore> OpenAsync(
        CloudStateStoreContext context,
        CancellationToken cancellationToken = default)
    {
        ICloudStateStore opened = await _inner.OpenAsync(context, cancellationToken).ConfigureAwait(false);
        if (Interlocked.CompareExchange(ref _store, opened, null) is not null)
        {
            await opened.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("The CfSharp state session has already opened a store.");
        }

        return opened;
    }
}

namespace MirrorPulse.Core.CloudFiles;

public interface IMirrorPulseSyncRootUnregistrar
{
    void Unregister(string path);
}

/// <summary>
/// Performs explicit account removal in the safe order: unregister first, clear MP state second.
/// </summary>
public sealed class MirrorPulseAccountRemovalService
{
    private readonly IMirrorPulseSyncRootUnregistrar _unregistrar;
    private readonly Func<CancellationToken, ValueTask> _clearState;

    public MirrorPulseAccountRemovalService(
        IMirrorPulseSyncRootUnregistrar unregistrar,
        Func<CancellationToken, ValueTask> clearState)
    {
        ArgumentNullException.ThrowIfNull(unregistrar);
        ArgumentNullException.ThrowIfNull(clearState);
        _unregistrar = unregistrar;
        _clearState = clearState;
    }

    public async ValueTask RemoveAsync(
        string syncRootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(syncRootPath);
        _unregistrar.Unregister(System.IO.Path.GetFullPath(syncRootPath.Trim()));
        await _clearState(cancellationToken).ConfigureAwait(false);
    }
}

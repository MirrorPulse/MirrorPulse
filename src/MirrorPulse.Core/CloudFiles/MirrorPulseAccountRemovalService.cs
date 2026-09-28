using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public interface IMirrorPulseSyncRootUnregistrar
{
    void Unregister(string path);
}

/// <summary>
/// Removes one registered MirrorPulse sync root through the native Cloud Files API.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class CfSharpSyncRootUnregistrar : IMirrorPulseSyncRootUnregistrar
{
    public void Unregister(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        CloudSyncRoot.Open(System.IO.Path.GetFullPath(path.Trim())).Unregister();
    }
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

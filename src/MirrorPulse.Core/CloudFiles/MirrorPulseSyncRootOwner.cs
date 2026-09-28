using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Coordinates one current-user owner for the persistent MirrorPulse sync root.
/// </summary>
public sealed class MirrorPulseSyncRootOwner : IDisposable
{
    private readonly ICurrentUserOwnerLock _ownerLock;

    public MirrorPulseSyncRootOwner(ICurrentUserOwnerLock ownerLock)
    {
        ArgumentNullException.ThrowIfNull(ownerLock);
        _ownerLock = ownerLock;
    }

    public bool IsHeld => _ownerLock.IsHeld;

    public static MirrorPulseSyncRootOwner CreateDefault() =>
        new(CurrentUserOwnerLock.Create("sync-root"));

    public bool TryAcquire(TimeSpan timeout) => _ownerLock.TryAcquire(timeout);

    public void EnsureHeld()
    {
        if (!_ownerLock.IsHeld)
        {
            throw new InvalidOperationException("The current user does not own the MirrorPulse Sync Root lock.");
        }
    }

    public void Dispose() => _ownerLock.Release();
}

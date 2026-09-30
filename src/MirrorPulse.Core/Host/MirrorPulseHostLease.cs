namespace MirrorPulse.Core.Host;

/// <summary>
/// Owns the current user's single MirrorPulse Host process.
/// </summary>
public sealed class MirrorPulseHostLease : IDisposable
{
    private readonly CurrentUserOwnerLock _ownerLock;
    private bool _disposed;

    private MirrorPulseHostLease(CurrentUserOwnerLock ownerLock)
    {
        _ownerLock = ownerLock;
    }

    public bool IsHeld => _ownerLock.IsHeld;

    public static MirrorPulseHostLease CreateDefault() =>
        new(CurrentUserOwnerLock.Create("host"));

    public bool TryAcquire(TimeSpan timeout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _ownerLock.TryAcquire(timeout);
    }

    public void EnsureHeld()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_ownerLock.IsHeld)
        {
            throw new InvalidOperationException("The current user does not own the MirrorPulse Host lease.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ownerLock.Dispose();
        _disposed = true;
    }
}

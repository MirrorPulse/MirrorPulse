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

    public void EnsureConsistent(
        MirrorPulseSyncRootDefinition definition,
        MirrorPulseSyncRootRegistrationState persistedState)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(persistedState);
        if (!_ownerLock.IsHeld)
        {
            throw new InvalidOperationException("The current user does not own the MirrorPulse Sync Root lock.");
        }

        var persistedPath = Path.GetFullPath(persistedState.Path);
        if (!string.Equals(persistedPath, definition.Path, StringComparison.OrdinalIgnoreCase)
            || persistedState.ProviderId != definition.ProviderId
            || !string.Equals(persistedState.ProviderVersion, definition.ProviderVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The persisted Sync Root registration does not match the configured MirrorPulse root.");
        }
    }

    public void Dispose() => _ownerLock.Release();
}

namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Delivers a newly detected pending conflict to the UI or notification host immediately.
/// </summary>
public sealed class MirrorPulseConflictNotificationBridge
{
    private readonly Func<MirrorPulseConflictRecord, CancellationToken, ValueTask> _notify;

    public MirrorPulseConflictNotificationBridge(
        Func<MirrorPulseConflictRecord, CancellationToken, ValueTask> notify)
    {
        ArgumentNullException.ThrowIfNull(notify);
        _notify = notify;
    }

    public ValueTask NotifyAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        if (!conflict.IsPending)
        {
            throw new InvalidOperationException("Only pending conflicts can trigger an immediate notification.");
        }

        return _notify(conflict, cancellationToken);
    }
}

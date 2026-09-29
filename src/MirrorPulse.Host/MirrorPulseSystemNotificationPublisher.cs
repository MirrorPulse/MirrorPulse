using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using MirrorPulse.Core.Conflicts;

namespace MirrorPulse.Host;

/// <summary>Publishes conflict notifications even when the WinUI process is not open.</summary>
public sealed class MirrorPulseSystemNotificationPublisher : IDisposable
{
    private bool _registered;
    private bool _disposed;

    public MirrorPulseSystemNotificationPublisher()
    {
        try
        {
            AppNotificationManager.Default.Register();
            _registered = true;
        }
        catch (Exception)
        {
            // Notification support is optional on development and restricted Windows hosts.
        }
    }

    public ValueTask PublishAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_registered)
        {
            return ValueTask.CompletedTask;
        }

        AppNotification notification = new AppNotificationBuilder()
            .AddArgument("action", "open-conflicts")
            .AddArgument("conflictId", conflict.ConflictId.ToString("D"))
            .AddText("MirrorPulse file conflict")
            .AddText($"{conflict.RelativePath} needs your attention.")
            .BuildNotification();
        try
        {
            AppNotificationManager.Default.Show(notification);
        }
        catch (Exception)
        {
            // A toast failure must not interrupt remote synchronization or conflict persistence.
        }

        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_registered)
        {
            AppNotificationManager.Default.Unregister();
        }

        _disposed = true;
    }
}

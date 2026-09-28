using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using MirrorPulse.Core.Host;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace MirrorPulse.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _window;
    private DispatcherQueueTimer? _notificationTimer;
    private readonly HashSet<string> _shownNotifications = new(StringComparer.Ordinal);
    private bool _checkingNotifications;

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        _notificationTimer = _window.DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(2);
        _notificationTimer.Tick += NotificationTimer_Tick;
        _notificationTimer.Start();
    }

    private async void NotificationTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        if (_checkingNotifications || _window is null)
        {
            return;
        }

        _checkingNotifications = true;
        try
        {
            MirrorPulseAppStatusResponse status = await MirrorPulseAppStatusPipe.RequestAsync();
            MirrorPulseAppNotification? next = status.Notifications.FirstOrDefault(item =>
                !item.Snoozed && !_shownNotifications.Contains(item.ConflictId));
            if (next is null)
            {
                return;
            }

            bool snooze = await _window.ShowConflictNotificationAsync(next);
            if (snooze)
            {
                await MirrorPulseAppStatusPipe.SnoozeAsync(Guid.Parse(next.ConflictId));
            }

            _shownNotifications.Add(next.ConflictId);
        }
        catch (Exception)
        {
            // The Host may be offline while WinUI remains open; the next tick retries.
        }
        finally
        {
            _checkingNotifications = false;
        }
    }
}


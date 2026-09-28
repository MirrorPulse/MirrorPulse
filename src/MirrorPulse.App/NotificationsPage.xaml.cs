using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MirrorPulse.Core.Host;

namespace MirrorPulse.App;

/// <summary>Reads durable pending conflict projections through the current-user Host channel.</summary>
public sealed partial class NotificationsPage : Page
{
    private IReadOnlyList<MirrorPulseAppNotification> _notifications = [];

    public NotificationsPage()
    {
        InitializeComponent();
        Loaded += NotificationsPage_Loaded;
    }

    private async void NotificationsPage_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void NotificationsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int index = NotificationsList.SelectedIndex;
        SnoozeButton.IsEnabled = index >= 0 && index < _notifications.Count && !_notifications[index].Snoozed;
    }

    private async void SnoozeButton_Click(object sender, RoutedEventArgs e)
    {
        int index = NotificationsList.SelectedIndex;
        if (index < 0 || index >= _notifications.Count || _notifications[index].Snoozed)
        {
            return;
        }

        try
        {
            MirrorPulseAppStatusResponse status = await MirrorPulseAppStatusPipe.SnoozeAsync(
                Guid.Parse(_notifications[index].ConflictId));
            Show(status);
        }
        catch (Exception exception)
        {
            NotificationsMessageText.Text = $"Could not snooze notification: {exception.Message}";
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            Show(await MirrorPulseAppStatusPipe.RequestAsync());
        }
        catch (Exception exception)
        {
            NotificationsMessageText.Text = $"Could not read notifications: {exception.Message}";
        }
    }

    private void Show(MirrorPulseAppStatusResponse status)
    {
        _notifications = status.Notifications;
        NotificationsList.ItemsSource = _notifications.Select(item =>
            $"{item.RelativePath}  ·  {item.DetectedAt:yyyy-MM-dd HH:mm}  ·  " +
            (item.Snoozed ? "Snoozed" : "Needs attention")).ToArray();
        SnoozeButton.IsEnabled = false;
        NotificationsMessageText.Text = _notifications.Count == 0
            ? "No pending conflict notifications."
            : $"{_notifications.Count(item => !item.Snoozed)} conflict notifications need attention.";
    }
}

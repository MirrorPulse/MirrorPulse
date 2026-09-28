using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MirrorPulse.Core.Host;

namespace MirrorPulse.App;

/// <summary>Shows local product state and CfSharp's authoritative sync journal.</summary>
public sealed partial class SyncStatusPage : Page
{
    public SyncStatusPage()
    {
        InitializeComponent();
        Loaded += SyncStatusPage_Loaded;
    }

    private async void SyncStatusPage_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void NotificationsButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(NotificationsPage));

    private async Task RefreshAsync()
    {
        try
        {
            MirrorPulseAppStatusResponse status = await MirrorPulseAppStatusPipe.RequestAsync();
            PendingUploadsText.Text = $"Pending uploads: {status.PendingUploads}";
            PendingConflictsText.Text = $"Pending remote conflicts: {status.PendingRemoteConflicts}";
            InstanceStatusList.ItemsSource = status.Instances.Select(instance =>
            {
                string checkpoint = instance.CursorFingerprint is not null
                    ? $"Cursor {instance.CursorFingerprint} ({instance.CursorUpdatedAt:yyyy-MM-dd HH:mm})"
                    : "No remote cursor";
                string error = instance.LastErrorCode is null
                    ? string.Empty : $"  ·  Last error: {instance.LastErrorCode}";
                return $"{instance.DisplayName}  ·  {instance.Phase}  ·  {checkpoint}{error}";
            }).ToArray();
            StatusMessageText.Text = status.Instances.Count == 0
                ? "No Adapter instances are configured."
                : "Cursor values are shown as fingerprints to keep opaque tokens private.";
        }
        catch (Exception exception)
        {
            StatusMessageText.Text = $"Could not read sync status: {exception.Message}";
        }
    }
}

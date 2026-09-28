using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MirrorPulse.Core;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.State;

namespace MirrorPulse.App;

/// <summary>
/// Lists installed Adapter packages and their configured instances.
/// </summary>
public sealed partial class InstalledAdaptersPage : Page
{
    public InstalledAdaptersPage()
    {
        InitializeComponent();
        Loaded += InstalledAdaptersPage_Loaded;
    }

    private async void InstalledAdaptersPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            string dataRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name);
            string syncRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ProductInfo.Name);
            var paths = new MirrorPulseStoragePaths(syncRoot, dataRoot);
            MirrorPulseAdapterTopology topology = await MirrorPulseProductCatalog
                .ReadAdapterTopologySnapshotAsync(paths);
            InstalledAdaptersList.ItemsSource = topology.Installations.Select(installation =>
            {
                string instances = string.Join(", ", topology.Instances
                    .Where(instance => instance.InstallId == installation.InstallId)
                    .Select(instance => instance.DisplayName));
                return $"{installation.AdapterId}  ·  {installation.Version}  ·  {installation.InstallId}" +
                    (instances.Length == 0 ? string.Empty : $"  ·  {instances}");
            }).ToArray();
            EmptyStateText.Visibility = topology.Installations.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception exception)
        {
            EmptyStateText.Text = $"Could not read installed Adapters: {exception.Message}";
            EmptyStateText.Visibility = Visibility.Visible;
        }
    }

    private void SyncStatusButton_Click(object sender, RoutedEventArgs e) =>
        Frame.Navigate(typeof(SyncStatusPage));
}

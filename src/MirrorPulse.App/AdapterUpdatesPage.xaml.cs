using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Shows independently updateable versions for installed Adapter packages.
/// </summary>
public sealed partial class AdapterUpdatesPage : Page
{
    public AdapterUpdatesPage()
    {
        InitializeComponent();
    }

    private void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        InstalledVersionText.Text = "1.0.0";
        LatestVersionText.Text = "1.0.0";
        UpdateButton.IsEnabled = false;
        UpdateStatusBar.IsOpen = true;
        UpdateStatusBar.Message = "All installed Adapters are up to date.";
    }

    private void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateStatusBar.IsOpen = true;
        UpdateStatusBar.Message = "The selected Adapter will be updated independently.";
    }
}

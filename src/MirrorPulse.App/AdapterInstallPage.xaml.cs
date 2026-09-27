using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Provides the user flow for installing a packaged Adapter.
/// </summary>
public sealed partial class AdapterInstallPage : Page
{
    public AdapterInstallPage()
    {
        InitializeComponent();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        InstallStatusBar.IsOpen = true;
        InstallStatusBar.Message = "Select a .mpadapter package to continue.";
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallStatusBar.IsOpen = true;
        InstallStatusBar.Message = string.IsNullOrWhiteSpace(PackagePathTextBox.Text)
            ? "Choose a .mpadapter package first."
            : "The Adapter package is ready to be verified.";
    }
}

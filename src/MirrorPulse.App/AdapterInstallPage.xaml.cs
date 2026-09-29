using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MirrorPulse.Core.Host;

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

    private async void InstallButton_Click(object sender, RoutedEventArgs e)
    {
        InstallStatusBar.IsOpen = true;
        if (string.IsNullOrWhiteSpace(PackagePathTextBox.Text))
        {
            InstallStatusBar.Message = "Choose a .mpadapter package first.";
            return;
        }

        InstallButton.IsEnabled = false;
        try
        {
            await MirrorPulseAppStatusPipe.InstallAsync(PackagePathTextBox.Text.Trim());
            InstallStatusBar.Severity = InfoBarSeverity.Success;
            InstallStatusBar.Message = "The Adapter was verified, installed, and added as a new instance.";
            Frame.Navigate(typeof(InstalledAdaptersPage));
        }
        catch (Exception exception)
        {
            InstallStatusBar.Severity = InfoBarSeverity.Error;
            InstallStatusBar.Message = $"Could not install the Adapter: {exception.Message}";
        }
        finally
        {
            InstallButton.IsEnabled = true;
        }
    }
}

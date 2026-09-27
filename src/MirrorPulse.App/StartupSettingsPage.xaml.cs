using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Controls optional startup behavior and displays the managed sync root name.
/// </summary>
public sealed partial class StartupSettingsPage : Page
{
    public StartupSettingsPage()
    {
        InitializeComponent();
    }

    private void StartWithWindowsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        StartupStatusBar.IsOpen = true;
        StartupStatusBar.Message = StartWithWindowsToggle.IsOn
            ? "MirrorPulse will start when you sign in."
            : "MirrorPulse will not start automatically.";
    }
}

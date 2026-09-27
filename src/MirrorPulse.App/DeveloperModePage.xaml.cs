using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Controls the global developer-mode switch used for unsigned Adapter packages.
/// </summary>
public sealed partial class DeveloperModePage : Page
{
    public DeveloperModePage()
    {
        InitializeComponent();
    }

    private void DeveloperModeToggle_Toggled(object sender, RoutedEventArgs e)
    {
        DeveloperModeStatusText.Text = DeveloperModeToggle.IsOn
            ? "Developer mode is enabled for this user."
            : "Developer mode is disabled; signed packages are required.";
    }
}

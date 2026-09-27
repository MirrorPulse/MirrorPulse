using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Lists installed Adapter packages and their configured instances.
/// </summary>
public sealed partial class InstalledAdaptersPage : Page
{
    public InstalledAdaptersPage()
    {
        InitializeComponent();
    }

    private void AdapterEnableToggle_Toggled(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        EmptyStateText.Text = AdapterEnableToggle.IsOn
            ? "The selected Adapter will start with MirrorPulse."
            : "The selected Adapter is offline until enabled.";
    }
}

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MirrorPulse.App;

/// <summary>
/// Collects the user-owned settings required to create one Adapter instance.
/// </summary>
public sealed partial class InstanceConfigurationPage : Page
{
    public InstanceConfigurationPage()
    {
        InitializeComponent();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        StepText.Text = "Step 1 of 3: Choose a source";
        ConfigurationProgress.Value = 1;
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        StepText.Text = "Step 2 of 3: Confirm settings";
        ConfigurationProgress.Value = 2;
    }
}

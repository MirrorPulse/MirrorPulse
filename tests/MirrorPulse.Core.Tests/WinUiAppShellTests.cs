namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WinUiAppShellTests
{
    [TestMethod]
    public void WinUiAppShellUsesPackagedWindowsAppSdkAndBothSupportedRuntimes()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App"));
        var project = File.ReadAllText(Path.Combine(root, "MirrorPulse.App.csproj"));

        StringAssert.Contains(project, "<UseWinUI>true</UseWinUI>");
        StringAssert.Contains(project, "<EnableMsixTooling>true</EnableMsixTooling>");
        StringAssert.Contains(project, "<RuntimeIdentifiers>win-x64;win-arm64</RuntimeIdentifiers>");
        StringAssert.Contains(project, "Microsoft.WindowsAppSDK");
        Assert.IsTrue(File.Exists(Path.Combine(root, "App.xaml")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "MainWindow.xaml")));
        Assert.IsTrue(File.Exists(Path.Combine(root, "Package.appxmanifest")));
    }
}

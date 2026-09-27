namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class StartupSettingsPageTests
{
    [TestMethod]
    public void StartupSettingsPageExposesOptionalStartupAndSyncRoot()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/StartupSettingsPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "StartWithWindowsToggle");
        StringAssert.Contains(content, "SyncRootNameText");
        StringAssert.Contains(content, "MirrorPulse");
        StringAssert.Contains(content, "StartupStatusBar");
    }
}

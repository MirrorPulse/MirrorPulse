namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterUpdatesPageTests
{
    [TestMethod]
    public void AdapterUpdatesPageExposesIndependentVersionActions()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/AdapterUpdatesPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "InstalledVersionText");
        StringAssert.Contains(content, "LatestVersionText");
        StringAssert.Contains(content, "CheckForUpdatesButton");
        StringAssert.Contains(content, "UpdateButton");
    }
}

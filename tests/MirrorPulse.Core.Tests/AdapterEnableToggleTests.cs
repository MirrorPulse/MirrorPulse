namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterEnableToggleTests
{
    [TestMethod]
    public void InstalledAdaptersPageExposesEnableToggle()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/InstalledAdaptersPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "AdapterEnableToggle");
        StringAssert.Contains(content, "Toggled=\"AdapterEnableToggle_Toggled\"");
        StringAssert.Contains(File.ReadAllText(Path.ChangeExtension(path, ".xaml.cs")), "will start with MirrorPulse");
    }
}

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstallPageTests
{
    [TestMethod]
    public void AdapterInstallPageUsesMpadapterPackageFlow()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/AdapterInstallPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "x:Class=\"MirrorPulse.App.AdapterInstallPage\"");
        StringAssert.Contains(content, ".mpadapter");
        StringAssert.Contains(content, "BrowseButton");
        StringAssert.Contains(content, "InstallButton");
    }
}

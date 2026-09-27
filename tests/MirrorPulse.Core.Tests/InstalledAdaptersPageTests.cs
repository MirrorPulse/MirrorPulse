namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class InstalledAdaptersPageTests
{
    [TestMethod]
    public void InstalledAdaptersPageProvidesListAndEmptyState()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/InstalledAdaptersPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "x:Class=\"MirrorPulse.App.InstalledAdaptersPage\"");
        StringAssert.Contains(content, "InstalledAdaptersList");
        StringAssert.Contains(content, "No Adapters are installed yet.");
    }
}

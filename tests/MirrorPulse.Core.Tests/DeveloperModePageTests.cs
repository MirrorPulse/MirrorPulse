namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class DeveloperModePageTests
{
    [TestMethod]
    public void DeveloperModePageWarnsAboutUnsignedPackages()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/DeveloperModePage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "DeveloperModeToggle");
        StringAssert.Contains(content, "UnsignedPackageWarning");
        StringAssert.Contains(content, "Unsigned .mpadapter packages");
    }
}

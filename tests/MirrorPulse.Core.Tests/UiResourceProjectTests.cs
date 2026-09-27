namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class UiResourceProjectTests
{
    [TestMethod]
    public void UiResourceProjectTargetsWindowsAndHasLocaleRoot()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.UI.Resources/MirrorPulse.UI.Resources.csproj"));
        var stringsPath = Path.Combine(Path.GetDirectoryName(projectPath)!, "Strings");
        var project = File.ReadAllText(projectPath);

        StringAssert.Contains(project, "<TargetFramework>net10.0-windows</TargetFramework>");
        Assert.IsTrue(Directory.Exists(stringsPath));
    }
}

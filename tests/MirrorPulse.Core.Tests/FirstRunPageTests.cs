namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class FirstRunPageTests
{
    [TestMethod]
    public void FirstRunPageProvidesWelcomePrivacyAndGetStartedControls()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/MainPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "Welcome to MirrorPulse");
        StringAssert.Contains(content, "Your data stays local");
        StringAssert.Contains(content, "GetStartedButton");
        StringAssert.Contains(content, "GetStartedButton_Click");
    }
}

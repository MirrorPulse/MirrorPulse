namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class InstanceConfigurationPageTests
{
    [TestMethod]
    public void InstanceConfigurationPageExposesSourceAndCredentialFields()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/InstanceConfigurationPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "x:Class=\"MirrorPulse.App.InstanceConfigurationPage\"");
        StringAssert.Contains(content, "SourcePathTextBox");
        StringAssert.Contains(content, "CredentialReferenceTextBox");
        StringAssert.Contains(content, "ConfigurationProgress");
    }
}

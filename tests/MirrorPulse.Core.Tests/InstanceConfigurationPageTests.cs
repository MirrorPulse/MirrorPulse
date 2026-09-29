namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class InstanceConfigurationPageTests
{
    [TestMethod]
    public void InstanceConfigurationPageExposesSourceFolderAndSecureCredentialInputs()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.App/InstanceConfigurationPage.xaml"));
        var content = File.ReadAllText(path);

        StringAssert.Contains(content, "x:Class=\"MirrorPulse.App.InstanceConfigurationPage\"");
        StringAssert.Contains(content, "SourcePathTextBox");
        StringAssert.Contains(content, "RootLabelsPanel");
        StringAssert.Contains(content, "SecretBox");
        StringAssert.Contains(content, "CreateButton");
    }
}

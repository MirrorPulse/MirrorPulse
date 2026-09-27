namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CfSharpPreviewPackageTests
{
    [TestMethod]
    public void CoreReferencesThePinnedCfSharpPreviewPackage()
    {
        var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/MirrorPulse.Core/MirrorPulse.Core.csproj"));
        var packagePropsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Directory.Packages.props"));
        var project = File.ReadAllText(projectPath);
        var packageProps = File.ReadAllText(packagePropsPath);

        StringAssert.Contains(project, "<PackageReference Include=\"CfSharp\" />");
        StringAssert.Contains(packageProps, "<PackageVersion Include=\"CfSharp\" Version=\"0.1.0-preview.1\" />");
    }
}

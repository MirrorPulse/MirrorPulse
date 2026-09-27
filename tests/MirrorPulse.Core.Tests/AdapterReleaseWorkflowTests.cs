namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterReleaseWorkflowTests
{
    private static readonly string[] RequiredWorkflowTerms = [
        "workflow_dispatch:",
        "tags:",
        "win-x64",
        "win-arm64",
        "dotnet publish",
        ".mpadapter",
        "permissions:",
        "contents: write",
        "gh release create",
    ];

    [TestMethod]
    public void TemplateReleaseWorkflowBuildsBothPayloadsAndPublishesAnArtifact()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Adapters/.github/workflows/release.yml"));
        var content = File.ReadAllText(path);

        foreach (var term in RequiredWorkflowTerms)
        {
            StringAssert.Contains(content, term);
        }
    }
}

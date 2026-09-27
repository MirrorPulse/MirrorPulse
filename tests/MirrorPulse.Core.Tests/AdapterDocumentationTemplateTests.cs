namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterDocumentationTemplateTests
{
    private static readonly string[] RequiredSections = [
        "## Manifest contract",
        "## Worker process contract",
        "## Configuration and storage",
        "## Packaging and signing",
        "## Localization",
        "## Release checklist",
    ];

    [TestMethod]
    public void AdapterTemplateDocumentsTheAuthoringContract()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Adapters/template/README.md"));
        var content = File.ReadAllText(path);

        foreach (var section in RequiredSections)
        {
            StringAssert.Contains(content, section);
        }

        StringAssert.Contains(content, "Named Pipe");
        StringAssert.Contains(content, ".mpadapter");
        StringAssert.Contains(content, "en-US");
    }
}

using MirrorPulse.Core.Diagnostics;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CfSharpIssueTemplateBuilderTests
{
    [TestMethod]
    public void BuildEmitsAllRequiredIssueSections()
    {
        var markdown = CfSharpIssueTemplateBuilder.Build(new CfSharpIssueReport(
            "0.1.0-preview.1",
            "Open a placeholder and request hydration.",
            "The range is served.",
            "The callback fails.",
            "HRESULT 0x80000001",
            "Hydration is unavailable.",
            "Retry after restarting the Host.",
            "Investigating"));

        StringAssert.Contains(markdown, "# CfSharp preview issue");
        StringAssert.Contains(markdown, "## Native error");
        StringAssert.Contains(markdown, "0.1.0-preview.1");
        StringAssert.Contains(markdown, "Retry after restarting the Host.");
    }

    [TestMethod]
    public void BuildRejectsMissingReproduction()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CfSharpIssueTemplateBuilder.Build(new CfSharpIssueReport(
            "0.1.0-preview.1", " ", "expected", "actual", "", "impact", "", "open")));
    }
}

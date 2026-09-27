using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class DiagnosticEventTests
{
    [TestMethod]
    public void DiagnosticEventCarriesStructuredDiagnosticAndCorrelation()
    {
        var properties = new Dictionary<string, string> { ["operation"] = "sync" };
        var diagnostic = new Diagnostic("sync.failed", "The sync operation failed.", DiagnosticSeverity.Error);
        var diagnosticEvent = new DiagnosticEvent(
            Guid.NewGuid(),
            "MirrorPulse.Host",
            diagnostic,
            DateTimeOffset.UtcNow,
            "request-1",
            properties);
        properties["operation"] = "changed";

        Assert.AreEqual("MirrorPulse.Host", diagnosticEvent.Source);
        Assert.AreEqual("request-1", diagnosticEvent.CorrelationId);
        Assert.AreEqual("sync", diagnosticEvent.Properties["operation"]);
        Assert.AreSame(diagnostic, diagnosticEvent.Diagnostic);
    }

    [TestMethod]
    public void DiagnosticEventRejectsEmptySource()
    {
        var diagnostic = new Diagnostic("sync.failed", "The sync operation failed.", DiagnosticSeverity.Error);

        Assert.ThrowsExactly<ArgumentException>(() => new DiagnosticEvent(
            Guid.NewGuid(),
            "",
            diagnostic,
            DateTimeOffset.UtcNow));
    }
}

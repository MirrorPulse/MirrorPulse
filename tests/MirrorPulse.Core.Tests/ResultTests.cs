using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ResultTests
{
    [TestMethod]
    public void SuccessCarriesValueWithoutError()
    {
        var result = Result.Success("ready");

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.AreEqual("ready", result.Value);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void FailureCarriesStructuredErrorWithoutValue()
    {
        var error = new ErrorInfo(
            "auth.expired",
            "The credential has expired.",
            ErrorCategory.Authentication,
            retryable: false,
            authRequired: true,
            diagnosticId: "diag-123");
        var result = Result.Failure<string>(error);

        Assert.IsTrue(result.IsFailure);
        Assert.IsFalse(result.IsSuccess);
        Assert.AreSame(error, result.Error);
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = result.Value);
    }

    [TestMethod]
    public void DiagnosticsRequireStableCodeAndMessage()
    {
        var diagnostic = new Diagnostic("manifest.invalid", "The manifest is invalid.", DiagnosticSeverity.Error);

        Assert.AreEqual("manifest.invalid", diagnostic.Code);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.ThrowsExactly<ArgumentException>(() => new Diagnostic("", "message", DiagnosticSeverity.Error));
    }
}

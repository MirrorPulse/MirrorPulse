using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ErrorCodeTests
{
    [TestMethod]
    public void MpAndWorkerErrorCodesUseStableNamespaces()
    {
        var mpCodes = new[]
        {
            ErrorCodes.ManifestInvalid,
            ErrorCodes.PackageSignatureInvalid,
            ErrorCodes.RootNameConflict,
            ErrorCodes.ProtocolVersionUnsupported,
            ErrorCodes.ProtocolFrameTooLarge,
            ErrorCodes.WorkerUnavailable,
            ErrorCodes.WorkerTimeout,
            ErrorCodes.CapabilityUnsupported
        };
        var workerCodes = new[]
        {
            ErrorCodes.WorkerAuthenticationRequired,
            ErrorCodes.WorkerRemoteUnavailable,
            ErrorCodes.WorkerConflictDetected,
            ErrorCodes.WorkerOperationUnsupported,
            ErrorCodes.WorkerNativeFailure,
            ErrorCodes.WorkerProtocolViolation
        };

        Assert.IsTrue(mpCodes.All(code => code.StartsWith("mp.", StringComparison.Ordinal)));
        Assert.IsTrue(workerCodes.All(code => code.StartsWith("worker.", StringComparison.Ordinal)));
        Assert.AreEqual(mpCodes.Length + workerCodes.Length, mpCodes.Concat(workerCodes).Distinct(StringComparer.Ordinal).Count());
    }

    [TestMethod]
    public void ErrorCodeCanPopulateStructuredError()
    {
        var error = new ErrorInfo(
            ErrorCodes.WorkerAuthenticationRequired,
            "The Worker requires credentials.",
            ErrorCategory.Authentication,
            authRequired: true);

        Assert.AreEqual(ErrorCodes.WorkerAuthenticationRequired, error.Code);
        Assert.IsTrue(error.AuthRequired);
    }
}

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Stable error codes exchanged by MP and Adapter Workers.
/// </summary>
public static class ErrorCodes
{
    public const string ManifestInvalid = "mp.manifest.invalid";
    public const string PackageSignatureInvalid = "mp.package.signatureInvalid";
    public const string RootNameConflict = "mp.root.nameConflict";
    public const string ProtocolVersionUnsupported = "mp.protocol.versionUnsupported";
    public const string ProtocolFrameTooLarge = "mp.protocol.frameTooLarge";
    public const string WorkerUnavailable = "mp.worker.unavailable";
    public const string WorkerTimeout = "mp.worker.timeout";
    public const string CapabilityUnsupported = "mp.capability.unsupported";
    public const string WorkerAuthenticationRequired = "worker.authentication.required";
    public const string WorkerRemoteUnavailable = "worker.remote.unavailable";
    public const string WorkerConflictDetected = "worker.remote.conflict";
    public const string WorkerOperationUnsupported = "worker.operation.unsupported";
    public const string WorkerNativeFailure = "worker.native.failure";
    public const string WorkerProtocolViolation = "worker.protocol.violation";
}

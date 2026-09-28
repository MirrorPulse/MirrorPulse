using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

/// <summary>
/// Builds a durable operation for a local file deletion.
/// </summary>
public static class MirrorPulseLocalDeleteUpload
{
    public static MirrorPulseQueuedUpload Create(
        Guid operationId,
        InstanceId instanceId,
        string relativePath,
        DateTimeOffset createdAt,
        IReadOnlyList<Guid>? dependencies = null) =>
        new(
            operationId,
            instanceId,
            MirrorPulseUploadOperationKind.Delete,
            relativePath,
            null,
            ReadOnlyMemory<byte>.Empty,
            createdAt,
            dependencies);
}

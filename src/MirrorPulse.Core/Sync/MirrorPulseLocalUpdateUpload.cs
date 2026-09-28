using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

/// <summary>
/// Builds a durable operation for modified bytes of an existing local file.
/// </summary>
public static class MirrorPulseLocalUpdateUpload
{
    public static MirrorPulseQueuedUpload Create(
        Guid operationId,
        InstanceId instanceId,
        string relativePath,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset createdAt,
        IReadOnlyList<Guid>? dependencies = null) =>
        new(
            operationId,
            instanceId,
            MirrorPulseUploadOperationKind.Update,
            relativePath,
            null,
            payload,
            createdAt,
            dependencies);
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

/// <summary>
/// Builds a durable operation for a local file move or rename.
/// </summary>
public static class MirrorPulseLocalMoveUpload
{
    public static MirrorPulseQueuedUpload Create(
        Guid operationId,
        InstanceId instanceId,
        string relativePath,
        string destinationPath,
        DateTimeOffset createdAt,
        IReadOnlyList<Guid>? dependencies = null) =>
        new(
            operationId,
            instanceId,
            MirrorPulseUploadOperationKind.Move,
            relativePath,
            destinationPath,
            ReadOnlyMemory<byte>.Empty,
            createdAt,
            dependencies);
}

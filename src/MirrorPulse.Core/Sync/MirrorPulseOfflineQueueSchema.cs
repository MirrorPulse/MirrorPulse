using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

/// <summary>
/// The durable operation kinds emitted by the local Cloud Files change pipeline.
/// </summary>
public enum MirrorPulseUploadOperationKind
{
    Create,
    Update,
    Move,
    Delete,
}

/// <summary>
/// Lifecycle state persisted with an offline upload operation.
/// </summary>
public enum MirrorPulseUploadOperationState
{
    Pending,
    InFlight,
    WaitingToRetry,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>
/// Versioned queue record owned by MP and scoped to one Adapter instance.
/// </summary>
public sealed record MirrorPulseQueuedUpload
{
    public const int CurrentSchemaVersion = 1;

    public MirrorPulseQueuedUpload(
        Guid operationId,
        InstanceId instanceId,
        MirrorPulseUploadOperationKind kind,
        string relativePath,
        string? destinationPath,
        ReadOnlyMemory<byte> payload,
        DateTimeOffset createdAt,
        IReadOnlyList<Guid>? dependencies = null,
        MirrorPulseUploadOperationState state = MirrorPulseUploadOperationState.Pending,
        int attempt = 0,
        DateTimeOffset? nextAttemptAt = null)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An upload operation ID cannot be empty.", nameof(operationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (relativePath.Contains('\0'))
        {
            throw new ArgumentException("Upload paths cannot contain a null character.", nameof(relativePath));
        }

        if (kind == MirrorPulseUploadOperationKind.Move)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        }
        else if (destinationPath is not null)
        {
            throw new ArgumentException("Only move operations may specify a destination path.", nameof(destinationPath));
        }

        dependencies ??= [];
        if (dependencies.Any(dependency => dependency == Guid.Empty))
        {
            throw new ArgumentException("Queue dependencies cannot contain an empty operation ID.", nameof(dependencies));
        }

        if (dependencies.Distinct().Count() != dependencies.Count)
        {
            throw new ArgumentException("Queue dependencies must be unique.", nameof(dependencies));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        if (state is MirrorPulseUploadOperationState.Succeeded or MirrorPulseUploadOperationState.Cancelled
            && nextAttemptAt is not null)
        {
            throw new ArgumentException("Terminal operations cannot have a next attempt time.", nameof(nextAttemptAt));
        }

        OperationId = operationId;
        InstanceId = instanceId;
        Kind = kind;
        RelativePath = relativePath.Trim();
        DestinationPath = destinationPath?.Trim();
        Payload = payload.ToArray();
        CreatedAt = createdAt;
        Dependencies = new ReadOnlyCollection<Guid>(dependencies.ToArray());
        State = state;
        Attempt = attempt;
        NextAttemptAt = nextAttemptAt;
    }

    public static int SchemaVersion => CurrentSchemaVersion;

    public Guid OperationId { get; }

    public InstanceId InstanceId { get; }

    public MirrorPulseUploadOperationKind Kind { get; }

    public string RelativePath { get; }

    public string? DestinationPath { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public DateTimeOffset CreatedAt { get; }

    public IReadOnlyList<Guid> Dependencies { get; }

    public MirrorPulseUploadOperationState State { get; }

    public int Attempt { get; }

    public DateTimeOffset? NextAttemptAt { get; }
}

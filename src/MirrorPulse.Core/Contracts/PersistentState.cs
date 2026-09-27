using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Durable checkpoint and queue counters for one Adapter Instance.
/// </summary>
public sealed record InstancePersistentState
{
    public InstancePersistentState(
        string? localChangeCursor,
        string? remoteChangeCursor,
        long localSequence,
        int pendingOperationCount,
        int conflictCount,
        DateTimeOffset? lastSuccessfulSync)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(localSequence);
        ArgumentOutOfRangeException.ThrowIfNegative(pendingOperationCount);
        ArgumentOutOfRangeException.ThrowIfNegative(conflictCount);
        LocalChangeCursor = localChangeCursor;
        RemoteChangeCursor = remoteChangeCursor;
        LocalSequence = localSequence;
        PendingOperationCount = pendingOperationCount;
        ConflictCount = conflictCount;
        LastSuccessfulSync = lastSuccessfulSync;
    }

    public string? LocalChangeCursor { get; }

    public string? RemoteChangeCursor { get; }

    public long LocalSequence { get; }

    public int PendingOperationCount { get; }

    public int ConflictCount { get; }

    public DateTimeOffset? LastSuccessfulSync { get; }
}

/// <summary>
/// Version-one durable state snapshot kept outside the Cloud Files sync root.
/// </summary>
public sealed record MirrorPulsePersistentState
{
    public const int CurrentSchemaVersion = 1;

    public MirrorPulsePersistentState(
        int schemaVersion,
        DateTimeOffset savedAt,
        IReadOnlyDictionary<InstanceId, InstancePersistentState> instances)
    {
        ArgumentNullException.ThrowIfNull(instances);
        SchemaVersion = schemaVersion;
        SavedAt = savedAt;
        Instances = new ReadOnlyDictionary<InstanceId, InstancePersistentState>(
            new Dictionary<InstanceId, InstancePersistentState>(instances));
    }

    public int SchemaVersion { get; }

    public DateTimeOffset SavedAt { get; }

    public IReadOnlyDictionary<InstanceId, InstancePersistentState> Instances { get; }
}

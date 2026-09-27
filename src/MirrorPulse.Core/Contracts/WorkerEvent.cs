using System.Text.Json;

namespace MirrorPulse.Core.Contracts;

public enum WorkerEventKind
{
    Started,
    Ready,
    Progress,
    RemoteChange,
    RootChanged,
    CredentialRequired,
    Stopped,
    Faulted
}

/// <summary>
/// Ordered event emitted by one Worker session.
/// </summary>
public sealed record WorkerEvent
{
    public WorkerEvent(
        Guid eventId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        long sequence,
        WorkerEventKind kind,
        DateTimeOffset occurredAt,
        JsonElement payload)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("A Worker event ID cannot be empty.", nameof(eventId));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        EventId = eventId;
        InstanceId = instanceId;
        WorkerSessionId = workerSessionId;
        Sequence = sequence;
        Kind = kind;
        OccurredAt = occurredAt;
        Payload = payload.Clone();
    }

    public Guid EventId { get; }

    public InstanceId InstanceId { get; }

    public WorkerSessionId WorkerSessionId { get; }

    public long Sequence { get; }

    public WorkerEventKind Kind { get; }

    public DateTimeOffset OccurredAt { get; }

    public JsonElement Payload { get; }
}

namespace MirrorPulse.Core.Contracts;

public enum HealthStatus
{
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Periodic liveness request sent over the Worker control channel.
/// </summary>
public sealed record HeartbeatMessage : ControlRequest
{
    public HeartbeatMessage(
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        long sequence,
        DateTimeOffset sentAt)
        : base("Heartbeat", requestId, instanceId, workerSessionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Sequence = sequence;
        SentAt = sentAt;
    }

    public long Sequence { get; }

    public DateTimeOffset SentAt { get; }
}

/// <summary>
/// Health response correlated with a heartbeat request.
/// </summary>
public sealed record HealthMessage : ControlResponse
{
    public HealthMessage(
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        HealthStatus status,
        long sequence,
        DateTimeOffset observedAt,
        string? detail = null)
        : base("Health", requestId, instanceId, workerSessionId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        Status = status;
        Sequence = sequence;
        ObservedAt = observedAt;
        Detail = detail;
    }

    public HealthStatus Status { get; }

    public long Sequence { get; }

    public DateTimeOffset ObservedAt { get; }

    public string? Detail { get; }
}

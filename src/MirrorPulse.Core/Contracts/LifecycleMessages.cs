namespace MirrorPulse.Core.Contracts;

public enum CancelReason
{
    UserRequested,
    Timeout,
    Replaced,
    HostStopping,
    ProtocolError
}

public enum ShutdownReason
{
    UserRequested,
    HostStopping,
    Update,
    Fault,
    Restart
}

/// <summary>
/// Requests cancellation of one in-flight control operation.
/// </summary>
public sealed record CancelMessage : ControlRequest
{
    public CancelMessage(
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        Guid targetRequestId,
        CancelReason reason,
        bool force)
        : base("Cancel", requestId, instanceId, workerSessionId)
    {
        if (targetRequestId == Guid.Empty)
        {
            throw new ArgumentException("A cancellation target ID cannot be empty.", nameof(targetRequestId));
        }

        TargetRequestId = targetRequestId;
        Reason = reason;
        Force = force;
    }

    public Guid TargetRequestId { get; }

    public CancelReason Reason { get; }

    public bool Force { get; }
}

/// <summary>
/// Requests an orderly Worker shutdown, optionally followed by a restart.
/// </summary>
public sealed record ShutdownMessage : ControlRequest
{
    public ShutdownMessage(
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        ShutdownReason reason,
        TimeSpan gracePeriod,
        bool restartAfterExit)
        : base("Shutdown", requestId, instanceId, workerSessionId)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(gracePeriod, TimeSpan.Zero);

        Reason = reason;
        GracePeriod = gracePeriod;
        RestartAfterExit = restartAfterExit;
    }

    public ShutdownReason Reason { get; }

    public TimeSpan GracePeriod { get; }

    public bool RestartAfterExit { get; }
}

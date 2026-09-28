namespace MirrorPulse.Core.Sync;

/// <summary>
/// Applies the allowed transitions for one durable upload operation.
/// </summary>
public static class MirrorPulseUploadOperationStateMachine
{
    public static MirrorPulseQueuedUpload Start(MirrorPulseQueuedUpload operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureState(operation, MirrorPulseUploadOperationState.Pending, MirrorPulseUploadOperationState.WaitingToRetry);
        return Rebuild(operation, MirrorPulseUploadOperationState.InFlight, operation.Attempt, null);
    }

    public static MirrorPulseQueuedUpload MarkRetry(
        MirrorPulseQueuedUpload operation,
        DateTimeOffset retryAt)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureState(operation, MirrorPulseUploadOperationState.InFlight);
        return Rebuild(
            operation,
            MirrorPulseUploadOperationState.WaitingToRetry,
            checked(operation.Attempt + 1),
            retryAt);
    }

    public static MirrorPulseQueuedUpload Succeed(MirrorPulseQueuedUpload operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureState(operation, MirrorPulseUploadOperationState.InFlight);
        return Rebuild(operation, MirrorPulseUploadOperationState.Succeeded, operation.Attempt, null);
    }

    public static MirrorPulseQueuedUpload Fail(MirrorPulseQueuedUpload operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureState(operation, MirrorPulseUploadOperationState.InFlight);
        return Rebuild(operation, MirrorPulseUploadOperationState.Failed, operation.Attempt, null);
    }

    public static MirrorPulseQueuedUpload Cancel(MirrorPulseQueuedUpload operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        EnsureState(
            operation,
            MirrorPulseUploadOperationState.Pending,
            MirrorPulseUploadOperationState.WaitingToRetry,
            MirrorPulseUploadOperationState.InFlight);
        return Rebuild(operation, MirrorPulseUploadOperationState.Cancelled, operation.Attempt, null);
    }

    private static MirrorPulseQueuedUpload Rebuild(
        MirrorPulseQueuedUpload operation,
        MirrorPulseUploadOperationState state,
        int attempt,
        DateTimeOffset? nextAttemptAt) => new(
            operation.OperationId,
            operation.InstanceId,
            operation.Kind,
            operation.RelativePath,
            operation.DestinationPath,
            operation.Payload,
            operation.CreatedAt,
            operation.Dependencies,
            state,
            attempt,
            nextAttemptAt);

    private static void EnsureState(
        MirrorPulseQueuedUpload operation,
        params MirrorPulseUploadOperationState[] allowed)
    {
        if (!allowed.Contains(operation.State))
        {
            throw new InvalidOperationException(
                $"Operation {operation.OperationId:D} cannot transition from {operation.State}.");
        }
    }
}

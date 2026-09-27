namespace MirrorPulse.Core.Workers;

public enum WorkerTerminationOutcome
{
    AlreadyExited,
    Graceful,
    Forced,
}

public sealed record WorkerTerminationPolicy
{
    public WorkerTerminationPolicy(TimeSpan gracefulTimeout, uint forcedExitCode = 1)
    {
        if (gracefulTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(gracefulTimeout), "The graceful termination timeout must be positive.");
        }

        GracefulTimeout = gracefulTimeout;
        ForcedExitCode = forcedExitCode;
    }

    public TimeSpan GracefulTimeout { get; }

    public uint ForcedExitCode { get; }
}

public sealed record WorkerTerminationResult(WorkerTerminationOutcome Outcome, int ProcessId, int? ExitCode);

/// <summary>
/// Applies graceful waiting followed by Job Object tree termination.
/// </summary>
public static class WorkerProcessTerminator
{
    public static async Task<WorkerTerminationResult> StopAsync(
        WorkerProcessHandle worker,
        WorkerJobObject job,
        WorkerTerminationPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(policy);
        var processId = worker.ProcessId;
        if (worker.Process.HasExited)
        {
            return new WorkerTerminationResult(WorkerTerminationOutcome.AlreadyExited, processId, worker.Process.ExitCode);
        }

        var waitForExit = worker.WaitForExitAsync(cancellationToken);
        var gracefulWait = Task.Delay(policy.GracefulTimeout, cancellationToken);
        var completed = await Task.WhenAny(waitForExit, gracefulWait).ConfigureAwait(false);
        if (completed == waitForExit)
        {
            await waitForExit.ConfigureAwait(false);
            return new WorkerTerminationResult(WorkerTerminationOutcome.Graceful, processId, worker.Process.ExitCode);
        }

        job.Terminate(policy.ForcedExitCode);
        await worker.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return new WorkerTerminationResult(WorkerTerminationOutcome.Forced, processId, worker.Process.ExitCode);
    }
}

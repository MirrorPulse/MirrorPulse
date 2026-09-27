using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public sealed record WorkerRestartPolicy
{
    public WorkerRestartPolicy(int maxAttempts, TimeSpan rollingWindow, TimeSpan backoff)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxAttempts);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(rollingWindow, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(backoff, TimeSpan.Zero);

        MaxAttempts = maxAttempts;
        RollingWindow = rollingWindow;
        Backoff = backoff;
    }

    public int MaxAttempts { get; }

    public TimeSpan RollingWindow { get; }

    public TimeSpan Backoff { get; }
}

public interface IWorkerRestartStarter
{
    ValueTask StartAsync(InstallId installId, CancellationToken cancellationToken = default);
}

public sealed record WorkerRestartResult(bool Started, int Attempt, DateTimeOffset? RetryAfter);

/// <summary>
/// Applies per-installation rolling-window limits to crash restarts.
/// </summary>
public sealed class WorkerRestartCoordinator
{
    private readonly IWorkerRestartStarter _starter;
    private readonly WorkerRestartPolicy _policy;
    private readonly Dictionary<InstallId, Queue<DateTimeOffset>> _attempts = new();

    public WorkerRestartCoordinator(IWorkerRestartStarter starter, WorkerRestartPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(starter);
        ArgumentNullException.ThrowIfNull(policy);
        _starter = starter;
        _policy = policy;
    }

    public async Task<WorkerRestartResult> TryRestartAsync(
        InstallId installId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (!_attempts.TryGetValue(installId, out var attempts))
        {
            attempts = new Queue<DateTimeOffset>();
            _attempts[installId] = attempts;
        }

        while (attempts.Count > 0 && now - attempts.Peek() >= _policy.RollingWindow)
        {
            attempts.Dequeue();
        }

        if (attempts.Count >= _policy.MaxAttempts)
        {
            return new WorkerRestartResult(false, attempts.Count, attempts.Peek().Add(_policy.RollingWindow));
        }

        if (attempts.Count > 0 && _policy.Backoff > TimeSpan.Zero)
        {
            await Task.Delay(_policy.Backoff, cancellationToken).ConfigureAwait(false);
        }

        attempts.Enqueue(now);
        await _starter.StartAsync(installId, cancellationToken).ConfigureAwait(false);
        return new WorkerRestartResult(true, attempts.Count, null);
    }
}

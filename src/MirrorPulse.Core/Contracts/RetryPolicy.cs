namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Deterministic retry delay policy declared by MP for a Worker operation.
/// </summary>
public sealed record BackoffPolicy
{
    public BackoffPolicy(TimeSpan initialDelay, TimeSpan maximumDelay, double multiplier = 2.0, bool jitter = false)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, initialDelay);
        if (double.IsNaN(multiplier) || double.IsInfinity(multiplier) || multiplier < 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        }

        InitialDelay = initialDelay;
        MaximumDelay = maximumDelay;
        Multiplier = multiplier;
        Jitter = jitter;
    }

    public TimeSpan InitialDelay { get; }

    public TimeSpan MaximumDelay { get; }

    public double Multiplier { get; }

    public bool Jitter { get; }
}

/// <summary>
/// Retry instruction returned for a transient operation failure.
/// </summary>
public sealed record RetryAfterDirective
{
    public RetryAfterDirective(TimeSpan delay, int attempt, DateTimeOffset retryAt, string? reason = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        Delay = delay;
        Attempt = attempt;
        RetryAt = retryAt;
        Reason = reason;
    }

    public TimeSpan Delay { get; }

    public int Attempt { get; }

    public DateTimeOffset RetryAt { get; }

    public string? Reason { get; }
}

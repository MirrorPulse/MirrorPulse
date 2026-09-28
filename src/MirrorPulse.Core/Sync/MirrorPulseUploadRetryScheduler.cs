using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

/// <summary>
/// Calculates bounded retry instructions for transient upload failures.
/// </summary>
public sealed class MirrorPulseUploadRetryScheduler
{
    private readonly BackoffPolicy _policy;

    public MirrorPulseUploadRetryScheduler(BackoffPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
    }

    public RetryAfterDirective Schedule(
        int previousAttempt,
        DateTimeOffset now,
        string? reason = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previousAttempt);
        var attempt = checked(previousAttempt + 1);
        var delay = CalculateDelay(attempt);
        return new RetryAfterDirective(delay, attempt, now + delay, reason);
    }

    private TimeSpan CalculateDelay(int attempt)
    {
        if (_policy.InitialDelay == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var exponent = Math.Pow(_policy.Multiplier, attempt - 1);
        var ticks = _policy.InitialDelay.Ticks * exponent;
        if (double.IsNaN(ticks) || double.IsInfinity(ticks) || ticks >= _policy.MaximumDelay.Ticks)
        {
            return _policy.MaximumDelay;
        }

        var boundedTicks = Math.Max(0, (long)Math.Ceiling(ticks));
        return TimeSpan.FromTicks(Math.Min(boundedTicks, _policy.MaximumDelay.Ticks));
    }
}

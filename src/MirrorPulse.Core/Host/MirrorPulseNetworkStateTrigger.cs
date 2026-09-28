namespace MirrorPulse.Core.Host;

public sealed record MirrorPulseNetworkTransition(
    bool WasOnline,
    bool IsOnline,
    bool DrainRequested);

/// <summary>
/// Serializes network transitions and requests one queue drain per offline-to-online edge.
/// </summary>
public sealed class MirrorPulseNetworkStateTrigger : IDisposable
{
    private readonly Func<CancellationToken, ValueTask> _drain;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _isOnline;

    public MirrorPulseNetworkStateTrigger(
        bool initialOnline,
        Func<CancellationToken, ValueTask> drain)
    {
        ArgumentNullException.ThrowIfNull(drain);
        _isOnline = initialOnline;
        _drain = drain;
    }

    public bool IsOnline => _isOnline;

    public async ValueTask<MirrorPulseNetworkTransition> ObserveAsync(
        bool isOnline,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var wasOnline = _isOnline;
            if (wasOnline == isOnline)
            {
                return new(wasOnline, isOnline, false);
            }

            _isOnline = isOnline;
            var drainRequested = isOnline;
            if (drainRequested)
            {
                await _drain(cancellationToken).ConfigureAwait(false);
            }

            return new(wasOnline, isOnline, drainRequested);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

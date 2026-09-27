namespace MirrorPulse.Core.Transport;

public enum TransportConnectionState
{
    Connected,
    TimedOut,
    Disconnected,
}

/// <summary>
/// Inactivity threshold used by a transport connection monitor.
/// </summary>
public sealed record TransportTimeoutPolicy
{
    public TransportTimeoutPolicy(TimeSpan inactivityTimeout)
    {
        if (inactivityTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(inactivityTimeout), "The inactivity timeout must be positive.");
        }

        InactivityTimeout = inactivityTimeout;
    }

    public TimeSpan InactivityTimeout { get; }
}

/// <summary>
/// Tracks activity and detects timeout or explicit transport disconnects.
/// </summary>
public sealed class TransportConnectionMonitor
{
    private readonly TransportTimeoutPolicy _policy;
    private DateTimeOffset _lastActivity;
    private TransportConnectionState _state;

    public TransportConnectionMonitor(DateTimeOffset connectedAt, TransportTimeoutPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        _policy = policy;
        _lastActivity = connectedAt;
        _state = TransportConnectionState.Connected;
    }

    public DateTimeOffset LastActivity => _lastActivity;

    public TransportConnectionState State => _state;

    public void MarkActivity(DateTimeOffset at)
    {
        EnsureNotBeforeLastActivity(at);
        _lastActivity = at;
        _state = TransportConnectionState.Connected;
    }

    public void MarkConnected(DateTimeOffset at)
    {
        _lastActivity = at;
        _state = TransportConnectionState.Connected;
    }

    public void MarkDisconnected() => _state = TransportConnectionState.Disconnected;

    public TransportConnectionState Check(DateTimeOffset at)
    {
        if (_state == TransportConnectionState.Disconnected)
        {
            return _state;
        }

        EnsureNotBeforeLastActivity(at);
        if (at - _lastActivity >= _policy.InactivityTimeout)
        {
            _state = TransportConnectionState.TimedOut;
        }

        return _state;
    }

    private void EnsureNotBeforeLastActivity(DateTimeOffset at)
    {
        if (at < _lastActivity)
        {
            throw new ArgumentOutOfRangeException(nameof(at), "Transport timestamps cannot move backwards.");
        }
    }
}

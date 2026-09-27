namespace MirrorPulse.Core.Transport;

/// <summary>
/// Thread-safe byte window used to apply backpressure to one binary stream.
/// </summary>
public sealed class StreamWindow
{
    private readonly object _gate = new();
    private readonly long _maximumBytes;
    private long _inFlightBytes;

    public StreamWindow(long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        _maximumBytes = maximumBytes;
    }

    public long MaximumBytes => _maximumBytes;

    public long InFlightBytes
    {
        get
        {
            lock (_gate)
            {
                return _inFlightBytes;
            }
        }
    }

    public long AvailableBytes
    {
        get
        {
            lock (_gate)
            {
                return _maximumBytes - _inFlightBytes;
            }
        }
    }

    public bool TryReserve(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_gate)
        {
            if (bytes > _maximumBytes - _inFlightBytes)
            {
                return false;
            }

            _inFlightBytes += bytes;
            return true;
        }
    }

    public void Release(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        lock (_gate)
        {
            if (bytes > _inFlightBytes)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes), "A stream window cannot release more bytes than are in flight.");
            }

            _inFlightBytes -= bytes;
        }
    }
}

using System.Collections.Concurrent;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Links caller cancellation with explicit protocol cancellation for in-flight requests.
/// </summary>
public sealed class RequestCancellationRegistry : IDisposable
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _requests = new();
    private int _disposed;

    public int Count => _requests.Count;

    public CancellationToken Register(Guid requestId, CancellationToken callerToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request ID cannot be empty.", nameof(requestId));
        }

        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
        if (!_requests.TryAdd(requestId, cancellation))
        {
            cancellation.Dispose();
            throw new InvalidOperationException($"A cancellation registration for '{requestId}' already exists.");
        }

        return cancellation.Token;
    }

    public bool Cancel(Guid requestId)
    {
        if (!_requests.TryRemove(requestId, out var cancellation))
        {
            return false;
        }

        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }

        return true;
    }

    public bool Remove(Guid requestId)
    {
        if (_requests.TryRemove(requestId, out var cancellation))
        {
            cancellation.Dispose();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            foreach (var pair in _requests)
            {
                if (_requests.TryRemove(pair.Key, out var cancellation))
                {
                    cancellation.Cancel();
                    cancellation.Dispose();
                }
            }
        }
    }
}

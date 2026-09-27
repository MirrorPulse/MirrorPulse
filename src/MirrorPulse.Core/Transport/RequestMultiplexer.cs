using System.Collections.Concurrent;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Correlates concurrent control requests with their responses by request ID.
/// </summary>
public sealed class RequestMultiplexer : IDisposable
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ControlFrameEnvelope>> _pending = new();
    private int _disposed;

    public int PendingCount => _pending.Count;

    public Task<ControlFrameEnvelope> Register(Guid requestId)
    {
        ThrowIfDisposed();
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request ID cannot be empty.", nameof(requestId));
        }

        var completion = new TaskCompletionSource<ControlFrameEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(requestId, completion))
        {
            throw new InvalidOperationException($"A request with ID '{requestId}' is already pending.");
        }

        return completion.Task;
    }

    public bool TryComplete(ControlFrameEnvelope response)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (_pending.TryRemove(response.RequestId, out var completion))
        {
            return completion.TrySetResult(response);
        }

        return false;
    }

    public bool Cancel(Guid requestId, CancellationToken cancellationToken = default)
    {
        if (_pending.TryRemove(requestId, out var completion))
        {
            return completion.TrySetCanceled(cancellationToken);
        }

        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            foreach (var pair in _pending)
            {
                if (_pending.TryRemove(pair.Key, out var completion))
                {
                    completion.TrySetCanceled();
                }
            }
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }
}

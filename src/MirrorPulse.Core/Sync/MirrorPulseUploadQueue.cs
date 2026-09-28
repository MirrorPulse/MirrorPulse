using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Sync;

public sealed record MirrorPulseUploadRequest(
    Guid OperationId,
    InstanceId InstanceId,
    string RelativePath,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset CreatedAt);

public interface IMirrorPulseUploadQueueStore
{
    ValueTask EnqueueAsync(MirrorPulseUploadRequest request, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<MirrorPulseUploadRequest>> TakeAsync(
        int maxCount,
        CancellationToken cancellationToken);

    ValueTask AcknowledgeAsync(Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Validates and forwards local file updates to the durable upload queue store.
/// </summary>
public sealed class MirrorPulseUploadDispatcher
{
    private readonly IMirrorPulseUploadQueueStore _store;

    public MirrorPulseUploadDispatcher(IMirrorPulseUploadQueueStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public ValueTask EnqueueAsync(
        MirrorPulseUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.OperationId == Guid.Empty)
        {
            throw new ArgumentException("Upload operations require a non-empty operation ID.", nameof(request));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(request.RelativePath);
        if (request.RelativePath.Contains('\0'))
        {
            throw new ArgumentException("Upload paths cannot contain a null character.", nameof(request));
        }

        return _store.EnqueueAsync(request, cancellationToken);
    }

    public ValueTask<IReadOnlyList<MirrorPulseUploadRequest>> TakeAsync(
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        return _store.TakeAsync(maxCount, cancellationToken);
    }

    public ValueTask AcknowledgeAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("An upload operation ID cannot be empty.", nameof(operationId));
        }

        return _store.AcknowledgeAsync(operationId, cancellationToken);
    }
}

/// <summary>
/// Small deterministic queue implementation for host startup and unit-test wiring.
/// Production hosts replace it with the persistent catalog-backed store.
/// </summary>
public sealed class InMemoryMirrorPulseUploadQueueStore : IMirrorPulseUploadQueueStore
{
    private readonly object _gate = new();
    private readonly List<MirrorPulseUploadRequest> _pending = [];

    public ValueTask EnqueueAsync(MirrorPulseUploadRequest request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var existing = _pending.FindIndex(item => item.OperationId == request.OperationId);
            if (existing >= 0)
            {
                var previous = _pending[existing];
                if (previous.InstanceId != request.InstanceId ||
                    previous.RelativePath != request.RelativePath ||
                    !previous.Payload.Span.SequenceEqual(request.Payload.Span))
                {
                    throw new InvalidOperationException("An upload operation ID cannot be reused for a different request.");
                }

                return ValueTask.CompletedTask;
            }

            _pending.Add(request);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<MirrorPulseUploadRequest>> TakeAsync(
        int maxCount,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);
        lock (_gate)
        {
            return ValueTask.FromResult<IReadOnlyList<MirrorPulseUploadRequest>>(
                _pending.Take(maxCount).ToArray());
        }
    }

    public ValueTask AcknowledgeAsync(Guid operationId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _pending.RemoveAll(item => item.OperationId == operationId);
        }

        return ValueTask.CompletedTask;
    }
}

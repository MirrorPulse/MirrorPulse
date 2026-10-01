using System.Text.Json;
using System.Threading.Channels;

namespace MirrorPulse.Core.Host;

/// <summary>Keeps remote apply waits off the Worker response reader, with a bounded ordered inbox.</summary>
public sealed class AdapterWorkerRemoteBatchInbox : IAsyncDisposable
{
    private readonly Channel<JsonElement> _inbox;
    private readonly Func<JsonElement, CancellationToken, ValueTask> _apply;
    private readonly CancellationTokenSource _shutdown;
    private readonly Task _processor;

    public AdapterWorkerRemoteBatchInbox(Func<JsonElement, CancellationToken, ValueTask> apply,
        CancellationToken cancellationToken, int capacity = 32)
    {
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _inbox = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processor = RunAsync();
    }

    public CancellationToken CancellationToken => _shutdown.Token;

    public void Post(JsonElement payload)
    {
        if (!_inbox.Writer.TryWrite(payload.Clone()))
            throw new InvalidDataException("The Worker remote batch inbox is full or closed.");
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (JsonElement payload in _inbox.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
                await _apply(payload, _shutdown.Token).ConfigureAwait(false);
        }
        catch
        {
            // Wake the response reader so the Worker session reports the actual apply failure.
            await _shutdown.CancelAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _inbox.Writer.TryComplete();
        await _shutdown.CancelAsync().ConfigureAwait(false);
        try { await _processor.ConfigureAwait(false); }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested) { }
        finally { _shutdown.Dispose(); }
    }
}

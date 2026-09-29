using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Continuously delivers supported file operations from CfSharp's durable local journal to the
/// selected Worker. The CfSharp acknowledgement is written only after the Worker confirms its
/// conditional upload, so a crash leaves the same operation available after restart.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseJournalUploadPump : IAsyncDisposable
{
    private readonly CloudLocalChangeFeed _feed;
    private readonly MirrorPulseJournalUploadSource _source;
    private readonly MirrorPulseJournalUploadCompletion _completion;
    private readonly MirrorPulseCfSharpStateSession _state;
    private readonly IMirrorPulseWorkerUploadTransport _uploads;
    private readonly IMirrorPulseWorkerStatTransport _stats;
    private readonly string _syncRootPath;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _loop;

    public MirrorPulseJournalUploadPump(
        CloudLocalChangeFeed feed,
        MirrorPulseRootRouter router,
        MirrorPulseProductCatalog catalog,
        IMirrorPulseWorkerUploadTransport uploads,
        IMirrorPulseWorkerStatTransport stats,
        MirrorPulseCfSharpStateSession state,
        string syncRootPath,
        Func<InstanceId, bool> mayDispatch,
        MirrorPulseJournalUploadCompletion completion)
    {
        _feed = feed ?? throw new ArgumentNullException(nameof(feed));
        _uploads = uploads ?? throw new ArgumentNullException(nameof(uploads));
        _stats = stats ?? throw new ArgumentNullException(nameof(stats));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        ArgumentException.ThrowIfNullOrWhiteSpace(syncRootPath);
        ArgumentNullException.ThrowIfNull(mayDispatch);
        _syncRootPath = Path.GetFullPath(syncRootPath);
        _completion = completion ?? throw new ArgumentNullException(nameof(completion));
        _source = new MirrorPulseJournalUploadSource(feed, router, catalog, mayDispatch, _completion);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (_loop is not null)
        {
            throw new InvalidOperationException("The local journal upload pump has already started.");
        }

        await _feed.StartAsync(cancellationToken).ConfigureAwait(false);
        _loop = Task.Run(() => RunAsync(_shutdown.Token), CancellationToken.None);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                MirrorPulseJournalUploadBatch batch;
                try
                {
                    batch = await _source.ReadPendingAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (batch.RequiresFullRescan || batch.ReadyCommands.Count == 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                bool dispatched = false;
                foreach (MirrorPulseWorkerChangeCommand command in batch.ReadyCommands)
                {
                    dispatched |= await DispatchAsync(command, cancellationToken).ConfigureAwait(false);
                }

                if (!dispatched)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async ValueTask<bool> DispatchAsync(
        MirrorPulseWorkerChangeCommand command,
        CancellationToken cancellationToken)
    {
        if (command.IsDirectory || command.Kind is not
            (MirrorPulseWorkerChangeKind.Create or MirrorPulseWorkerChangeKind.ContentUpdate))
        {
            return false;
        }

        string localPath = Path.GetFullPath(Path.Combine(_syncRootPath,
            command.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = _syncRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!localPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(localPath))
        {
            return false;
        }

        try
        {
            string relativePath = Path.GetRelativePath(_syncRootPath, localPath)
                .Replace(Path.DirectorySeparatorChar, '/');
            string? revision = await MirrorPulseJournalUploadRevisionGuard.ResolveAsync(
                _state.OpenStore, _stats, command, relativePath, cancellationToken)
                .ConfigureAwait(false);
            await using var content = new FileStream(localPath, FileMode.Open, FileAccess.Read,
                FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
            string uploadedRevision = await _uploads.UploadAsync(
                new MirrorPulseWorkerUploadRequest(command.InstanceId, command.RelativePath,
                    revision, content, content.Length), cancellationToken).ConfigureAwait(false);
            await _completion.AcknowledgeSuccessfulUploadAsync(command.OperationId, uploadedRevision,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _completion.DeferFailedUploadAsync(command.OperationId, DateTimeOffset.UtcNow,
                cancellationToken).ConfigureAwait(false);
            return true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        if (_loop is not null)
        {
            await _loop.ConfigureAwait(false);
        }

        await _feed.DisposeAsync().ConfigureAwait(false);
        _shutdown.Dispose();
    }
}

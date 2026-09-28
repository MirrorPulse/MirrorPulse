using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using CfSharp;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseJournalUploadBatch(
    IReadOnlyList<MirrorPulseWorkerChangeCommand> ReadyCommands,
    int DeferredCount,
    bool RequiresFullRescan);

/// <summary>
/// Reads upload intent from CfSharp's durable local journal. The MP catalog records only an
/// idempotency fingerprint, never a parallel operation sequence or file contents.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseJournalUploadSource
{
    private readonly CloudLocalChangeFeed _feed;
    private readonly MirrorPulseRootRouter _router;
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly Func<InstanceId, bool> _mayDispatch;

    public MirrorPulseJournalUploadSource(
        CloudLocalChangeFeed feed,
        MirrorPulseRootRouter router,
        MirrorPulseProductCatalog catalog,
        Func<InstanceId, bool> mayDispatch)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mayDispatch);
        _feed = feed;
        _router = router;
        _catalog = catalog;
        _mayDispatch = mayDispatch;
    }

    public async ValueTask<MirrorPulseJournalUploadBatch> ReadPendingAsync(
        CancellationToken cancellationToken = default)
    {
        CloudLocalChangeBatch batch = await _feed.ReadBatchAsync(cancellationToken).ConfigureAwait(false);
        MirrorPulseLocalBatchPlan plan = MirrorPulseLocalBatchMapper.Map(batch, _router);
        if (plan.RequiresFullRescan)
        {
            return new([], 0, true);
        }

        var ready = new List<MirrorPulseWorkerChangeCommand>(plan.Commands.Count);
        int deferred = 0;
        foreach (MirrorPulseWorkerChangeCommand command in plan.Commands)
        {
            byte[] fingerprint = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(command));
            await _catalog.TryRecordWorkerRequestAsync(
                command.OperationId,
                command.InstanceId,
                fingerprint,
                cancellationToken).ConfigureAwait(false);
            if (_mayDispatch(command.InstanceId))
            {
                ready.Add(command);
            }
            else
            {
                deferred++;
            }
        }

        return new(ready.AsReadOnly(), deferred, false);
    }
}

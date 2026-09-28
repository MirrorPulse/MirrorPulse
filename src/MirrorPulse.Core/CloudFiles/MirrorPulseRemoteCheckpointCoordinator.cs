using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Publishes a remote cursor only after CfSharp reports a fully applied batch.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseRemoteCheckpointCoordinator
{
    private readonly MirrorPulseRemoteBatchApplier _applier;
    private readonly MirrorPulseRemoteCursorStore _cursorStore;

    public MirrorPulseRemoteCheckpointCoordinator(
        MirrorPulseRemoteBatchApplier applier,
        MirrorPulseRemoteCursorStore cursorStore)
    {
        ArgumentNullException.ThrowIfNull(applier);
        ArgumentNullException.ThrowIfNull(cursorStore);
        _applier = applier;
        _cursorStore = cursorStore;
    }

    public async ValueTask<MirrorPulseRemoteBatchOutcome> ApplyAsync(
        InstanceId instanceId,
        CloudRemoteChangeBatch batch,
        CloudRemoteApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var outcome = await _applier.ApplyAsync(batch, options, cancellationToken).ConfigureAwait(false);
        if (!outcome.RequiresRetry && outcome.Status == CloudRemoteBatchStatus.Applied)
        {
            await _cursorStore.SaveAsync(instanceId, outcome.SafeCursor, cancellationToken).ConfigureAwait(false);
        }

        return outcome;
    }
}

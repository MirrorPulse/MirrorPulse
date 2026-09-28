using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public sealed record MirrorPulseLocalChangeSignal(bool RequiresFullRescan);

public sealed record MirrorPulseFullRescanResult(
    bool WasRequired,
    int ReconciledEntryCount);

/// <summary>
/// Runs a complete local reconciliation before acknowledging a CfSharp full-rescan request.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseFullRescanCoordinator
{
    private readonly Func<CancellationToken, ValueTask<int>> _rescan;
    private readonly Func<CancellationToken, ValueTask> _acknowledge;

    public MirrorPulseFullRescanCoordinator(
        Func<CancellationToken, ValueTask<int>> rescan,
        Func<CancellationToken, ValueTask> acknowledge)
    {
        ArgumentNullException.ThrowIfNull(rescan);
        ArgumentNullException.ThrowIfNull(acknowledge);
        _rescan = rescan;
        _acknowledge = acknowledge;
    }

    public static MirrorPulseFullRescanCoordinator For(
        CloudLocalChangeFeed feed,
        Func<CancellationToken, ValueTask<int>> rescan)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return new(rescan, feed.AcknowledgeFullRescanAsync);
    }

    public async ValueTask<MirrorPulseFullRescanResult> HandleAsync(
        MirrorPulseLocalChangeSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (!signal.RequiresFullRescan)
        {
            return new(false, 0);
        }

        var count = await _rescan(cancellationToken).ConfigureAwait(false);
        var normalizedCount = Math.Max(0, count);
        await _acknowledge(cancellationToken).ConfigureAwait(false);
        return new(true, normalizedCount);
    }

    public ValueTask<MirrorPulseFullRescanResult> HandleAsync(
        CloudLocalChangeBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return HandleAsync(new MirrorPulseLocalChangeSignal(batch.RequiresFullRescan), cancellationToken);
    }
}

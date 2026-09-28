using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>Connects CfSharp's durable rescan marker to MP's reconciliation policy.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseCfSharpFullRescanAdapter
{
    private readonly MirrorPulseFullRescanCoordinator _coordinator;

    public MirrorPulseCfSharpFullRescanAdapter(
        CloudLocalChangeFeed feed,
        Func<CancellationToken, ValueTask<int>> rescan)
    {
        ArgumentNullException.ThrowIfNull(feed);
        _coordinator = new MirrorPulseFullRescanCoordinator(
            rescan,
            feed.AcknowledgeFullRescanAsync);
    }

    public ValueTask<MirrorPulseFullRescanResult> HandleAsync(
        CloudLocalChangeBatch batch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return _coordinator.HandleAsync(
            new MirrorPulseLocalChangeSignal(batch.RequiresFullRescan),
            cancellationToken);
    }
}

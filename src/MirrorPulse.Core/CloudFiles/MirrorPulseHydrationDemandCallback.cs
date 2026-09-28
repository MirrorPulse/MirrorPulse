using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Describes the byte range requested when Cloud Files asks MirrorPulse to hydrate a file.
/// </summary>
public sealed record MirrorPulseHydrationDemand(
    string NormalizedPath,
    ReadOnlyMemory<byte> FileIdentity,
    long Offset,
    long Length);

public interface IMirrorPulseHydrationDemandHandler
{
    ValueTask<Stream> HandleAsync(
        MirrorPulseHydrationDemand demand,
        CancellationToken cancellationToken);
}

/// <summary>
/// Routes native Cloud Files hydration callbacks to the active Adapter worker.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseHydrationDemandCallback : ICloudFileContentProvider
{
    private readonly IMirrorPulseHydrationDemandHandler _handler;

    public MirrorPulseHydrationDemandCallback(IMirrorPulseHydrationDemandHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _handler = handler;
    }

    public ValueTask<Stream> HandleAsync(
        MirrorPulseHydrationDemand demand,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(demand);
        ArgumentException.ThrowIfNullOrWhiteSpace(demand.NormalizedPath);
        ArgumentOutOfRangeException.ThrowIfNegative(demand.Offset);
        ArgumentOutOfRangeException.ThrowIfNegative(demand.Length);
        return _handler.HandleAsync(demand, cancellationToken);
    }

    public ValueTask<Stream> OpenReadAsync(
        CloudFileFetchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HandleAsync(
            new MirrorPulseHydrationDemand(
                request.NormalizedPath,
                request.FileIdentity.ToArray(),
                request.Offset,
                request.Length),
            cancellationToken);
    }
}

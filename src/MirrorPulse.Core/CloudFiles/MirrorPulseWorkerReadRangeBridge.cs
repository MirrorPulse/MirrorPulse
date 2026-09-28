using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Carries a Cloud Files range request across the per-instance Worker boundary.
/// </summary>
public sealed record MirrorPulseWorkerReadRangeRequest(
    InstanceId InstanceId,
    string NormalizedPath,
    ReadOnlyMemory<byte> FileIdentity,
    long Offset,
    long Length);

public interface IMirrorPulseWorkerRangeTransport
{
    ValueTask<Stream> ReadRangeAsync(
        MirrorPulseWorkerReadRangeRequest request,
        CancellationToken cancellationToken);
}

/// <summary>
/// Binds the Cloud Files content reader contract to a single Adapter Worker instance.
/// </summary>
public sealed class MirrorPulseWorkerReadRangeBridge : IMirrorPulseContentReader
{
    private readonly InstanceId _instanceId;
    private readonly IMirrorPulseWorkerRangeTransport _transport;

    public MirrorPulseWorkerReadRangeBridge(
        InstanceId instanceId,
        IMirrorPulseWorkerRangeTransport transport)
    {
        _instanceId = instanceId;
        ArgumentNullException.ThrowIfNull(transport);
        _transport = transport;
    }

    public ValueTask<Stream> OpenReadAsync(
        MirrorPulseContentFetchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NormalizedPath);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Length);

        return _transport.ReadRangeAsync(
            new MirrorPulseWorkerReadRangeRequest(
                _instanceId,
                request.NormalizedPath,
                request.FileIdentity,
                request.Offset,
                request.Length),
            cancellationToken);
    }
}

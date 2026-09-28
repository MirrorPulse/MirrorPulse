using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>Carries one bounded Cloud Files range request to the selected Adapter Worker.</summary>
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

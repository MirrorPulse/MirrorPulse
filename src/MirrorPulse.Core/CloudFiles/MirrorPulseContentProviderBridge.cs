using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public sealed record MirrorPulseContentFetchRequest(
    string NormalizedPath,
    ReadOnlyMemory<byte> FileIdentity,
    long Offset,
    long Length);

public interface IMirrorPulseContentReader
{
    ValueTask<Stream> OpenReadAsync(MirrorPulseContentFetchRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Converts CfSharp hydration callbacks into an Adapter-owned range-read request.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseContentProviderBridge : ICloudFileContentProvider
{
    private readonly IMirrorPulseContentReader _reader;

    public MirrorPulseContentProviderBridge(IMirrorPulseContentReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    public ValueTask<Stream> OpenReadAsync(MirrorPulseContentFetchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NormalizedPath);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Offset);
        ArgumentOutOfRangeException.ThrowIfNegative(request.Length);
        return _reader.OpenReadAsync(request, cancellationToken);
    }

    public ValueTask<Stream> OpenReadAsync(CloudFileFetchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OpenReadAsync(
            new MirrorPulseContentFetchRequest(
                request.NormalizedPath,
                request.FileIdentity.ToArray(),
                request.Offset,
                request.Length),
            cancellationToken);
    }
}

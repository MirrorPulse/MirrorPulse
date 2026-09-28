using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public interface IMirrorPulseProviderEchoSink
{
    ValueTask SuppressAsync(
        CloudStateOperationKind operation,
        string relativePath,
        DateTimeOffset expiresAt,
        Guid? itemId,
        string? previousRelativePath,
        int remainingObservations,
        CancellationToken cancellationToken);
}

/// <summary>
/// Records provider-originated writes so the local feed does not enqueue them again.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseProviderEchoSuppressor
{
    private readonly IMirrorPulseProviderEchoSink _sink;

    public MirrorPulseProviderEchoSuppressor(IMirrorPulseProviderEchoSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    public static MirrorPulseProviderEchoSuppressor For(CloudLocalChangeFeed feed)
    {
        ArgumentNullException.ThrowIfNull(feed);
        return new(new NativeSink(feed));
    }

    public ValueTask SuppressAsync(
        CloudStateOperationKind operation,
        string relativePath,
        DateTimeOffset expiresAt,
        Guid? itemId = null,
        string? previousRelativePath = null,
        int remainingObservations = 1,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "Echo suppression must expire in the future.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(remainingObservations);
        return _sink.SuppressAsync(
            operation,
            relativePath,
            expiresAt,
            itemId,
            previousRelativePath,
            remainingObservations,
            cancellationToken);
    }

    private sealed class NativeSink : IMirrorPulseProviderEchoSink
    {
        private readonly CloudLocalChangeFeed _feed;

        public NativeSink(CloudLocalChangeFeed feed) => _feed = feed;

        public ValueTask SuppressAsync(
            CloudStateOperationKind operation,
            string relativePath,
            DateTimeOffset expiresAt,
            Guid? itemId,
            string? previousRelativePath,
            int remainingObservations,
            CancellationToken cancellationToken) =>
            _feed.SuppressProviderEchoAsync(
                operation,
                relativePath,
                expiresAt,
                itemId,
                previousRelativePath ?? string.Empty,
                remainingObservations,
                cancellationToken);
    }
}

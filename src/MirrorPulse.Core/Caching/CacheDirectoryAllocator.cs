using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Caching;

/// <summary>
/// Allocates per-instance file and transfer cache directories below an MP-owned root.
/// </summary>
public sealed class CacheDirectoryAllocator
{
    private readonly string _rootDirectory;

    public CacheDirectoryAllocator(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public CacheDirectoryAllocation Allocate(InstanceId instanceId, DateTimeOffset acquiredAt, TimeSpan leaseDuration)
    {
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration), "A cache lease duration must be positive.");
        }

        var instanceDirectory = Path.Combine(_rootDirectory, instanceId.Value.ToString("N"));
        var filesDirectory = Path.Combine(instanceDirectory, "files");
        var transfersDirectory = Path.Combine(instanceDirectory, "transfers");
        Directory.CreateDirectory(filesDirectory);
        Directory.CreateDirectory(transfersDirectory);

        var expiresAt = acquiredAt.Add(leaseDuration);
        var files = new CacheLease(Guid.NewGuid(), instanceId, CacheKind.Files, filesDirectory, acquiredAt, expiresAt, CacheCleanupMode.Immediate);
        var transfers = new CacheLease(Guid.NewGuid(), instanceId, CacheKind.Transfers, transfersDirectory, acquiredAt, expiresAt, CacheCleanupMode.Immediate);
        return new CacheDirectoryAllocation(instanceDirectory, files, transfers);
    }
}

public sealed class CacheDirectoryAllocation : IDisposable
{
    private readonly string _instanceDirectory;
    private int _released;

    internal CacheDirectoryAllocation(string instanceDirectory, CacheLease files, CacheLease transfers)
    {
        _instanceDirectory = instanceDirectory;
        Files = files;
        Transfers = transfers;
    }

    public CacheLease Files { get; }

    public CacheLease Transfers { get; }

    public IReadOnlyList<CacheCleanupRequest> Release(DateTimeOffset requestedAt, CacheCleanupReason reason = CacheCleanupReason.LeaseReleased)
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
        {
            return Array.Empty<CacheCleanupRequest>();
        }

        if (Directory.Exists(_instanceDirectory))
        {
            Directory.Delete(_instanceDirectory, recursive: true);
        }

        return new ReadOnlyCollection<CacheCleanupRequest>
        (
            [
                new CacheCleanupRequest(Files.LeaseId, reason, requestedAt, immediate: true),
                new CacheCleanupRequest(Transfers.LeaseId, reason, requestedAt, immediate: true),
            ]);
    }

    public void Dispose() => Release(DateTimeOffset.UtcNow);
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Caching;

/// <summary>
/// Deletes a released or expired cache lease only when it belongs to MP's cache root.
/// </summary>
public sealed class CacheLeaseCleanupService
{
    private readonly string _rootDirectory;

    public CacheLeaseCleanupService(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    public bool Cleanup(CacheLease lease, CacheCleanupRequest request, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(request);
        if (lease.LeaseId != request.LeaseId)
        {
            throw new ArgumentException("The cleanup request belongs to another lease.", nameof(request));
        }

        var expected = Path.GetFullPath(Path.Combine(
            _rootDirectory,
            lease.InstanceId.Value.ToString("N"),
            lease.Kind == CacheKind.Files ? "files" : "transfers"));
        if (!string.Equals(expected, Path.GetFullPath(lease.Path), StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The lease path is outside its managed cache location.");
        }

        if (!request.Immediate && now < lease.ExpiresAt)
        {
            return false;
        }

        if (!Directory.Exists(expected))
        {
            return false;
        }

        Directory.Delete(expected, recursive: true);
        return true;
    }
}

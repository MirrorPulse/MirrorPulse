namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Temporary cache area supplied to an Adapter Worker.
/// </summary>
public enum CacheKind
{
    Files,
    Transfers
}

/// <summary>
/// Cleanup behavior for temporary cache data.
/// </summary>
public enum CacheCleanupMode
{
    Immediate,
    OnExpiry
}

public enum CacheLeaseState
{
    Active,
    CleanupRequested,
    Released,
    Expired,
    Cleaned
}

public enum CacheCleanupReason
{
    LeaseReleased,
    LeaseExpired,
    InstanceRemoved,
    HostShutdown
}

/// <summary>
/// Lease describing a temporary path MP gives to one Adapter Instance.
/// </summary>
public sealed record CacheLease
{
    public CacheLease(
        Guid leaseId,
        InstanceId instanceId,
        CacheKind kind,
        string path,
        DateTimeOffset acquiredAt,
        DateTimeOffset expiresAt,
        CacheCleanupMode cleanupMode,
        CacheLeaseState state = CacheLeaseState.Active)
    {
        if (leaseId == Guid.Empty)
        {
            throw new ArgumentException("A cache lease ID cannot be empty.", nameof(leaseId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (expiresAt < acquiredAt)
        {
            throw new ArgumentException("A cache lease cannot expire before it is acquired.", nameof(expiresAt));
        }

        LeaseId = leaseId;
        InstanceId = instanceId;
        Kind = kind;
        Path = path;
        AcquiredAt = acquiredAt;
        ExpiresAt = expiresAt;
        CleanupMode = cleanupMode;
        State = state;
    }

    public Guid LeaseId { get; }

    public InstanceId InstanceId { get; }

    public CacheKind Kind { get; }

    public string Path { get; }

    public DateTimeOffset AcquiredAt { get; }

    public DateTimeOffset ExpiresAt { get; }

    public CacheCleanupMode CleanupMode { get; }

    public CacheLeaseState State { get; }
}

/// <summary>
/// Durable request to remove temporary cache data.
/// </summary>
public sealed record CacheCleanupRequest
{
    public CacheCleanupRequest(Guid leaseId, CacheCleanupReason reason, DateTimeOffset requestedAt, bool immediate)
    {
        if (leaseId == Guid.Empty)
        {
            throw new ArgumentException("A cache lease ID cannot be empty.", nameof(leaseId));
        }

        LeaseId = leaseId;
        Reason = reason;
        RequestedAt = requestedAt;
        Immediate = immediate;
    }

    public Guid LeaseId { get; }

    public CacheCleanupReason Reason { get; }

    public DateTimeOffset RequestedAt { get; }

    public bool Immediate { get; }
}

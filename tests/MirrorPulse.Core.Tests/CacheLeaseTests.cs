using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CacheLeaseTests
{
    [TestMethod]
    public void CacheLeaseDefaultsToImmediateCleanupPolicyWhenRequested()
    {
        var acquired = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero);
        var lease = new CacheLease(
            Guid.NewGuid(),
            InstanceId.New(),
            CacheKind.Files,
            "C:\\MirrorPulse\\cache\\files\\instance",
            acquired,
            acquired.AddMinutes(10),
            CacheCleanupMode.Immediate);

        var cleanup = new CacheCleanupRequest(lease.LeaseId, CacheCleanupReason.LeaseReleased, acquired, immediate: true);

        Assert.AreEqual(CacheLeaseState.Active, lease.State);
        Assert.AreEqual(CacheCleanupMode.Immediate, lease.CleanupMode);
        Assert.IsTrue(cleanup.Immediate);
    }

    [TestMethod]
    public void CacheLeaseRejectsEmptyIdsAndReversedExpiry()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.ThrowsExactly<ArgumentException>(() => new CacheLease(
            Guid.Empty,
            InstanceId.New(),
            CacheKind.Transfers,
            "C:\\MirrorPulse\\cache",
            now,
            now.AddMinutes(1),
            CacheCleanupMode.Immediate));
        Assert.ThrowsExactly<ArgumentException>(() => new CacheLease(
            Guid.NewGuid(),
            InstanceId.New(),
            CacheKind.Transfers,
            "C:\\MirrorPulse\\cache",
            now,
            now.AddMinutes(-1),
            CacheCleanupMode.Immediate));
    }
}

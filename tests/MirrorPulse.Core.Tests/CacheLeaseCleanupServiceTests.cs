using MirrorPulse.Core.Caching;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CacheLeaseCleanupServiceTests
{
    [TestMethod]
    public void ImmediateCleanupRemovesOneLeaseWithoutRemovingItsSibling()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cleanup-{Guid.NewGuid():N}");
        try
        {
            var now = DateTimeOffset.UtcNow;
            using var allocation = new CacheDirectoryAllocator(root).Allocate(InstanceId.New(), now, TimeSpan.FromMinutes(5));
            File.WriteAllText(Path.Combine(allocation.Transfers.Path, "pending.tmp"), "transfer");
            var request = new CacheCleanupRequest(allocation.Transfers.LeaseId, CacheCleanupReason.LeaseReleased, now, immediate: true);

            var removed = new CacheLeaseCleanupService(root).Cleanup(allocation.Transfers, request, now);

            Assert.IsTrue(removed);
            Assert.IsFalse(Directory.Exists(allocation.Transfers.Path));
            Assert.IsTrue(Directory.Exists(allocation.Files.Path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void CleanupRejectsAnUnmanagedLeasePath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cleanup-{Guid.NewGuid():N}");
        var now = DateTimeOffset.UtcNow;
        var lease = new CacheLease(Guid.NewGuid(), InstanceId.New(), CacheKind.Files, Path.GetTempPath(), now, now.AddMinutes(1), CacheCleanupMode.Immediate);
        var request = new CacheCleanupRequest(lease.LeaseId, CacheCleanupReason.LeaseReleased, now, immediate: true);

        Assert.ThrowsExactly<UnauthorizedAccessException>(() => new CacheLeaseCleanupService(root).Cleanup(lease, request, now));
    }
}

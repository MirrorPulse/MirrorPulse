using MirrorPulse.Core.Caching;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CacheDirectoryAllocatorTests
{
    [TestMethod]
    public void AllocatorCreatesAndImmediatelyReleasesPerInstanceCaches()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cache-{Guid.NewGuid():N}");
        try
        {
            var acquiredAt = DateTimeOffset.UtcNow;
            var instanceId = InstanceId.New();
            var allocator = new CacheDirectoryAllocator(root);
            using var allocation = allocator.Allocate(instanceId, acquiredAt, TimeSpan.FromMinutes(5));

            Assert.IsTrue(Directory.Exists(allocation.Files.Path));
            Assert.IsTrue(Directory.Exists(allocation.Transfers.Path));
            var cleanup = allocation.Release(acquiredAt.AddMinutes(1));

            Assert.HasCount(2, cleanup);
            Assert.IsTrue(cleanup.All(request => request.Immediate));
            Assert.IsFalse(Directory.Exists(allocation.Files.Path));
            Assert.HasCount(0, allocation.Release(acquiredAt.AddMinutes(2)));
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
    public void AllocatorRejectsNonPositiveLeaseDuration()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cache-{Guid.NewGuid():N}");
        try
        {
            var allocator = new CacheDirectoryAllocator(root);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => allocator.Allocate(InstanceId.New(), DateTimeOffset.UtcNow, TimeSpan.Zero));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

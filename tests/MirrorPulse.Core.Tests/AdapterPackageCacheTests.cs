using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterPackageCacheTests
{
    [TestMethod]
    public async Task CacheStoresBySha256AndRemovesExpiredPackages()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cache-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "source.mpadapter");
            await File.WriteAllTextAsync(source, "package-content");
            var digest = Sha256Digest.Compute("package-content"u8);
            var cache = new AdapterPackageCache(Path.Combine(root, "cache"));
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

            var entry = await cache.StoreAsync(source, digest, expiresAt);
            var hit = await cache.TryGetAsync(digest, DateTimeOffset.UtcNow);
            var removed = cache.CleanupExpired(expiresAt.AddSeconds(1));

            Assert.AreEqual(entry.PackagePath, hit!.PackagePath);
            Assert.AreEqual(1, removed);
            Assert.IsFalse(File.Exists(entry.PackagePath));
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
    public async Task CacheRejectsAHashMismatch()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-cache-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var source = Path.Combine(root, "source.mpadapter");
            await File.WriteAllTextAsync(source, "package-content");

            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                new AdapterPackageCache(Path.Combine(root, "cache")).StoreAsync(
                    source,
                    Sha256Digest.Compute("different"u8),
                    DateTimeOffset.UtcNow.AddMinutes(5)));
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

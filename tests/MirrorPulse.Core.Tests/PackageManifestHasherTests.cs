using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PackageManifestHasherTests
{
    [TestMethod]
    public void HashIsStableRegardlessOfInputOrder()
    {
        var first = new PackageFileEntry("b.txt", 1, Sha256Digest.Compute(new byte[] { 2 }));
        var second = new PackageFileEntry("a.txt", 1, Sha256Digest.Compute(new byte[] { 1 }));
        var left = PackageManifestHasher.Compute(new PackageFileManifest(new[] { first, second }));
        var right = PackageManifestHasher.Compute(new PackageFileManifest(new[] { second, first }));

        Assert.AreEqual(left, right);
        Assert.IsNotEmpty(PackageManifestHasher.Canonicalize(new PackageFileManifest(new[] { first, second })));
    }

    [TestMethod]
    public void HashChangesWhenFileMetadataChanges()
    {
        var original = new PackageFileManifest(new[] { new PackageFileEntry("worker.exe", 1, Sha256Digest.Compute(new byte[] { 1 })) });
        var changed = new PackageFileManifest(new[] { new PackageFileEntry("worker.exe", 2, Sha256Digest.Compute(new byte[] { 1 })) });

        Assert.AreNotEqual(PackageManifestHasher.Compute(original), PackageManifestHasher.Compute(changed));
    }
}

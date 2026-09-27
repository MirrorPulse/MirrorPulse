using System.Text;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PackageIntegrityTests
{
    [TestMethod]
    public void Sha256DigestComputesCanonicalHash()
    {
        var digest = Sha256Digest.Compute(Encoding.UTF8.GetBytes("MirrorPulse"));

        Assert.AreEqual("4E89DD8A57B990C46DFCF2BDF7A763A9E7B2066A373A048FAC02F89EB2204B1F", digest.Hexadecimal);
        Assert.AreEqual(digest, Sha256Digest.Parse(digest.ToString().ToLowerInvariant()));
    }

    [TestMethod]
    public void PackageFileManifestRejectsUnsafeOrDuplicateFiles()
    {
        var digest = Sha256Digest.Parse(new string('A', 64));

        Assert.ThrowsExactly<ArgumentException>(() => new PackageFileEntry("../manifest.json", 1, digest));
        Assert.ThrowsExactly<ArgumentException>(() => new PackageFileManifest(
        [
            new PackageFileEntry("manifest.json", 1, digest),
            new PackageFileEntry("MANIFEST.JSON", 1, digest)
        ]));
    }

    [TestMethod]
    public void PackageFileEntryPreservesLengthAndHash()
    {
        var digest = Sha256Digest.Parse(new string('B', 64));
        var entry = new PackageFileEntry("payload/win-x64/Adapter.exe", 42, digest);

        Assert.AreEqual(42, entry.Length);
        Assert.AreEqual(digest, entry.Sha256);
        Assert.IsTrue(PackageFileManifest.HashAlgorithm.StartsWith("SHA-", StringComparison.Ordinal));
    }
}

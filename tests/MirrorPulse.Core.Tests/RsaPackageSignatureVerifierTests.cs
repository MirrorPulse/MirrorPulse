using System.Security.Cryptography;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class RsaPackageSignatureVerifierTests
{
    [TestMethod]
    public async Task VerifierAcceptsValidDetachedSignature()
    {
        using var rsa = RSA.Create(2048);
        var manifest = CreateManifest();
        var signatureBytes = rsa.SignData(PackageManifestHasher.Canonicalize(manifest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signature = new AdapterPackageSignature(RsaPackageSignatureVerifier.Algorithm, "test-signer", signatureBytes);
        var verifier = new RsaPackageSignatureVerifier(rsa, "test-signer");

        var result = await verifier.VerifyAsync(new MemoryStream(new byte[] { 1 }), manifest, signature);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Value.IsValid);
    }

    [TestMethod]
    public async Task VerifierReportsInvalidSignatureWithoutExecutingPackage()
    {
        using var rsa = RSA.Create(2048);
        var manifest = CreateManifest();
        var signatureBytes = rsa.SignData(PackageManifestHasher.Canonicalize(manifest), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        signatureBytes[0] ^= 0xFF;
        var signature = new AdapterPackageSignature(RsaPackageSignatureVerifier.Algorithm, "test-signer", signatureBytes);
        var verifier = new RsaPackageSignatureVerifier(rsa, "test-signer");

        var result = await verifier.VerifyAsync(new MemoryStream(new byte[] { 1 }), manifest, signature);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.Value.IsValid);
    }

    private static PackageFileManifest CreateManifest() => new(
        new[]
        {
            new PackageFileEntry("manifest.json", 2, Sha256Digest.Compute(new byte[] { 1, 2 })),
        });
}

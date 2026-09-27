using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class PackageSignatureTests
{
    [TestMethod]
    public async Task SignatureVerifierContractCarriesVerificationResult()
    {
        var verifier = new AcceptingSignatureVerifier();
        var result = await verifier.VerifyAsync(
            Stream.Null,
            new PackageFileManifest(Array.Empty<PackageFileEntry>()),
            new AdapterPackageSignature("RSA-PSS-SHA256", "Example Publisher", [1, 2, 3]));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.Value.IsValid);
        Assert.AreEqual("Example Publisher", result.Value.Signer);
    }

    [TestMethod]
    public void SignatureMetadataCopiesSignatureBytes()
    {
        var signatureBytes = new byte[] { 1, 2, 3 };
        var signature = new AdapterPackageSignature("RSA-PSS-SHA256", "Example Publisher", signatureBytes);
        signatureBytes[0] = 9;

        Assert.AreEqual((byte)1, signature.Signature[0]);
        Assert.AreEqual("RSA-PSS-SHA256", signature.Algorithm);
    }

    private sealed class AcceptingSignatureVerifier : IAdapterPackageSignatureVerifier
    {
        public ValueTask<Result<PackageSignatureVerification>> VerifyAsync(
            Stream package,
            PackageFileManifest fileManifest,
            AdapterPackageSignature signature,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Success(new PackageSignatureVerification(true, signature.Signer, signature.Algorithm)));
    }
}

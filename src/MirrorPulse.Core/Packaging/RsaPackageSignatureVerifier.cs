using System.Security.Cryptography;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

/// <summary>
/// Verifies an RSA-SHA256 signature over the canonical package file manifest.
/// </summary>
public sealed class RsaPackageSignatureVerifier : IAdapterPackageSignatureVerifier
{
    public const string Algorithm = "RSA-SHA256";

    private readonly RSA _publicKey;
    private readonly string _signer;

    public RsaPackageSignatureVerifier(RSA publicKey, string signer)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(signer);
        _publicKey = publicKey;
        _signer = signer.Trim();
    }

    public ValueTask<Result<PackageSignatureVerification>> VerifyAsync(
        Stream package,
        PackageFileManifest fileManifest,
        AdapterPackageSignature signature,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(fileManifest);
        ArgumentNullException.ThrowIfNull(signature);
        cancellationToken.ThrowIfCancellationRequested();
        if (!package.CanRead)
        {
            return ValueTask.FromResult(Result.Failure<PackageSignatureVerification>(new ErrorInfo(
                ErrorCodes.PackageSignatureInvalid,
                "The Adapter package stream is not readable.",
                ErrorCategory.Storage)));
        }

        if (!string.Equals(signature.Algorithm, Algorithm, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(Result.Failure<PackageSignatureVerification>(new ErrorInfo(
                ErrorCodes.PackageSignatureInvalid,
                "The Adapter package signature algorithm is unsupported.",
                ErrorCategory.Unsupported,
                unsupported: true)));
        }

        if (!string.Equals(signature.Signer, _signer, StringComparison.Ordinal))
        {
            return ValueTask.FromResult(Result.Failure<PackageSignatureVerification>(new ErrorInfo(
                ErrorCodes.PackageSignatureInvalid,
                "The Adapter package signer is not trusted.",
                ErrorCategory.Authentication,
                authRequired: true)));
        }

        var valid = _publicKey.VerifyData(
            PackageManifestHasher.Canonicalize(fileManifest),
            signature.Signature.ToArray(),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return ValueTask.FromResult(Result.Success(new PackageSignatureVerification(valid, signature.Signer, signature.Algorithm)));
    }
}

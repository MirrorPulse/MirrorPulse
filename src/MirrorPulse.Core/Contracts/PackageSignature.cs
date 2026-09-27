using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Detached signature metadata for a canonical Adapter package file manifest.
/// </summary>
public sealed record AdapterPackageSignature
{
    public AdapterPackageSignature(string algorithm, string signer, ReadOnlySpan<byte> signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(signer);
        Algorithm = algorithm;
        Signer = signer;
        Signature = new ReadOnlyCollection<byte>(signature.ToArray());
    }

    public string Algorithm { get; }

    public string Signer { get; }

    public IReadOnlyList<byte> Signature { get; }
}

/// <summary>
/// Outcome returned after a package signature has been checked.
/// </summary>
public sealed record PackageSignatureVerification
{
    public PackageSignatureVerification(bool isValid, string signer, string algorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signer);
        ArgumentException.ThrowIfNullOrWhiteSpace(algorithm);
        IsValid = isValid;
        Signer = signer;
        Algorithm = algorithm;
    }

    public bool IsValid { get; }

    public string Signer { get; }

    public string Algorithm { get; }
}

/// <summary>
/// Boundary for verifying a detached signature without executing package code.
/// </summary>
public interface IAdapterPackageSignatureVerifier
{
    ValueTask<Result<PackageSignatureVerification>> VerifyAsync(
        Stream package,
        PackageFileManifest fileManifest,
        AdapterPackageSignature signature,
        CancellationToken cancellationToken = default);
}

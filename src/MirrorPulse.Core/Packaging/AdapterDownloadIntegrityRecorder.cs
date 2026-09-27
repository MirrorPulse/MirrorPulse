using System.Security.Cryptography;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterDownloadIntegrity(
    Uri SourceUri,
    string PackagePath,
    long Length,
    Sha256Digest Sha256);

/// <summary>
/// Records the immutable size and SHA-256 evidence for a downloaded package.
/// </summary>
public static class AdapterDownloadIntegrityRecorder
{
    public static async Task<AdapterDownloadIntegrity> ComputeAsync(
        DownloadedAdapterPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        await using var stream = File.OpenRead(package.PackagePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        var length = new FileInfo(package.PackagePath).Length;
        return new AdapterDownloadIntegrity(package.SourceUri, package.PackagePath, length, Sha256Digest.Parse(Convert.ToHexString(hash)));
    }
}

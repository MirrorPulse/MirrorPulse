using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

/// <summary>
/// Produces a stable UTF-8 representation and SHA-256 digest for package file metadata.
/// </summary>
public static class PackageManifestHasher
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static byte[] Canonicalize(PackageFileManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var entries = manifest.Files
            .OrderBy(file => file.Path, StringComparer.Ordinal)
            .Select(file => new HashEntry(file.Path, file.Length, file.Sha256.ToString()))
            .ToArray();
        return JsonSerializer.SerializeToUtf8Bytes(entries, SerializerOptions);
    }

    public static Sha256Digest Compute(PackageFileManifest manifest) => Sha256Digest.Compute(Canonicalize(manifest));

    private sealed record HashEntry(string Path, long Length, string Sha256);
}

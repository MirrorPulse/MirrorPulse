using System.Security.Cryptography;
using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterPackageCacheEntry(Sha256Digest Sha256, string PackagePath, DateTimeOffset ExpiresAt);

/// <summary>
/// Stores verified release packages by digest and removes expired entries.
/// </summary>
public sealed class AdapterPackageCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly string _rootDirectory;

    public AdapterPackageCache(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        Directory.CreateDirectory(_rootDirectory);
    }

    public string RootDirectory => _rootDirectory;

    public async Task<AdapterPackageCacheEntry> StoreAsync(
        string sourcePath,
        Sha256Digest expectedSha256,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var source = Path.GetFullPath(sourcePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The Adapter package was not found.", source);
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "A cached package must expire in the future.");
        }

        var actualSha256 = await ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
        if (actualSha256 != expectedSha256)
        {
            throw new InvalidDataException("The package hash does not match the expected cache key.");
        }

        var packagePath = Path.Combine(_rootDirectory, $"{expectedSha256}.mpadapter");
        var metadataPath = GetMetadataPath(expectedSha256);
        var temporaryPackage = packagePath + $".{Guid.NewGuid():N}.tmp";
        var temporaryMetadata = metadataPath + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporaryPackage, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            await using (var metadataStream = new FileStream(temporaryMetadata, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(metadataStream, new CacheMetadata(expectedSha256.ToString(), expiresAt), SerializerOptions, cancellationToken).ConfigureAwait(false);
                await metadataStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPackage, packagePath, overwrite: true);
            File.Move(temporaryMetadata, metadataPath, overwrite: true);
            return new AdapterPackageCacheEntry(expectedSha256, packagePath, expiresAt);
        }
        finally
        {
            if (File.Exists(temporaryPackage))
            {
                File.Delete(temporaryPackage);
            }

            if (File.Exists(temporaryMetadata))
            {
                File.Delete(temporaryMetadata);
            }
        }
    }

    public async Task<AdapterPackageCacheEntry?> TryGetAsync(
        Sha256Digest sha256,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var packagePath = Path.Combine(_rootDirectory, $"{sha256}.mpadapter");
        var metadataPath = GetMetadataPath(sha256);
        if (!File.Exists(packagePath) || !File.Exists(metadataPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(metadataPath);
        var metadata = await JsonSerializer.DeserializeAsync<CacheMetadata>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
        if (metadata is null || !Sha256Digest.TryParse(metadata.Sha256, out var storedSha256) || storedSha256 != sha256)
        {
            return null;
        }

        if (metadata.ExpiresAt <= now)
        {
            Remove(sha256);
            return null;
        }

        return new AdapterPackageCacheEntry(sha256, packagePath, metadata.ExpiresAt);
    }

    public void Remove(Sha256Digest sha256)
    {
        var packagePath = Path.Combine(_rootDirectory, $"{sha256}.mpadapter");
        var metadataPath = GetMetadataPath(sha256);
        if (File.Exists(packagePath))
        {
            File.Delete(packagePath);
        }

        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }
    }

    public int CleanupExpired(DateTimeOffset now)
    {
        var removed = 0;
        foreach (var metadataPath in Directory.EnumerateFiles(_rootDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<CacheMetadata>(File.ReadAllText(metadataPath), SerializerOptions);
                if (metadata is not null && Sha256Digest.TryParse(metadata.Sha256, out var sha256) && metadata.ExpiresAt <= now)
                {
                    Remove(sha256);
                    removed++;
                }
            }
            catch (JsonException)
            {
                File.Delete(metadataPath);
            }
        }

        return removed;
    }

    private string GetMetadataPath(Sha256Digest sha256) => Path.Combine(_rootDirectory, $"{sha256}.json");

    private static async Task<Sha256Digest> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Sha256Digest.Parse(Convert.ToHexString(hash));
    }

    private sealed record CacheMetadata(string Sha256, DateTimeOffset ExpiresAt);
}

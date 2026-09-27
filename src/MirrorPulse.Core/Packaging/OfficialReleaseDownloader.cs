namespace MirrorPulse.Core.Packaging;

public sealed record DownloadedAdapterPackage(Uri SourceUri, string PackagePath, long Length);

/// <summary>
/// Downloads an official `.mpadapter` release over HTTPS into a caller-owned path.
/// </summary>
public sealed class OfficialReleaseDownloader
{
    private readonly HttpClient _httpClient;

    public OfficialReleaseDownloader(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task<DownloadedAdapterPackage> DownloadAsync(
        Uri packageUri,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(packageUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        if (!string.Equals(packageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Official Adapter releases must be downloaded over HTTPS.", nameof(packageUri));
        }

        var destination = Path.GetFullPath(destinationPath);
        if (!string.Equals(Path.GetExtension(destination), ".mpadapter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Official Adapter releases must use the .mpadapter extension.", nameof(destinationPath));
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using var response = await _httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, destination, overwrite: true);
            return new DownloadedAdapterPackage(packageUri, destination, new FileInfo(destination).Length);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

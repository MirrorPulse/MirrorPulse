using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace MirrorPulse.Core.Adapters.WebDav;

public sealed record MirrorPulseWebDavRemoteEntry(
    string RelativePath,
    bool IsDirectory,
    long? Length,
    string? ETag);

public sealed record MirrorPulseWebDavReadResult(
    byte[] Content,
    long? TotalLength,
    string? ETag);

/// <summary>
/// Lists WebDAV directories through authenticated PROPFIND requests.
/// </summary>
public sealed class MirrorPulseWebDavDirectoryClient
{
    private static readonly XNamespace Dav = "DAV:";
    private const string PropertyRequest = "<propfind xmlns=\"DAV:\"><prop><resourcetype/><getcontentlength/><getetag/></prop></propfind>";
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly MirrorPulseWebDavCredential? _credential;

    public MirrorPulseWebDavDirectoryClient(
        HttpClient httpClient,
        Uri baseUri,
        MirrorPulseWebDavCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A WebDAV base URI must use HTTP or HTTPS.", nameof(baseUri));
        }

        _httpClient = httpClient;
        _baseUri = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        _credential = credential;
    }

    public async Task<IReadOnlyList<MirrorPulseWebDavRemoteEntry>> ListDirectoryAsync(
        string relativePath = "",
        CancellationToken cancellationToken = default)
    {
        var endpoint = Resolve(relativePath);
        using var request = MirrorPulseWebDavAuthenticator.CreateRequest(new HttpMethod("PROPFIND"), endpoint, _credential);
        request.Headers.TryAddWithoutValidation("Depth", "1");
        request.Content = new StringContent(PropertyRequest, Encoding.UTF8, "application/xml");
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.MultiStatus)
        {
            return ParseEntries(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), endpoint);
        }

        response.EnsureSuccessStatusCode();
        return ParseEntries(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false), endpoint);
    }

    public async Task<MirrorPulseWebDavReadResult> ReadRangeAsync(
        string relativePath,
        long offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var endpoint = Resolve(relativePath);
        using var request = MirrorPulseWebDavAuthenticator.CreateRequest(HttpMethod.Get, endpoint, _credential);
        request.Headers.Range = new RangeHeaderValue(offset, checked(offset + length - 1));
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var totalLength = response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength;
        return new MirrorPulseWebDavReadResult(content, totalLength, response.Headers.ETag?.Tag);
    }

    private Uri Resolve(string relativePath)
    {
        ArgumentNullException.ThrowIfNull(relativePath);
        if (Uri.TryCreate(relativePath, UriKind.Absolute, out _))
        {
            throw new ArgumentException("WebDAV paths must be relative to the configured base URI.", nameof(relativePath));
        }

        return new Uri(_baseUri, relativePath.TrimStart('/'));
    }

    private static ReadOnlyCollection<MirrorPulseWebDavRemoteEntry> ParseEntries(string xml, Uri endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        var endpointPath = endpoint.AbsolutePath.TrimEnd('/');
        var entries = new List<MirrorPulseWebDavRemoteEntry>();
        foreach (var response in document.Descendants(Dav + "response"))
        {
            var href = response.Element(Dav + "href")?.Value;
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            var entryUri = Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                ? absolute
                : new Uri(endpoint, href);
            if (string.Equals(entryUri.AbsolutePath.TrimEnd('/'), endpointPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var prop = response.Descendants(Dav + "prop").FirstOrDefault();
            var resourceType = prop?.Element(Dav + "resourcetype");
            var lengthElement = prop?.Element(Dav + "getcontentlength");
            var etag = prop?.Element(Dav + "getetag")?.Value;
            var relative = entryUri.AbsolutePath.StartsWith(endpointPath + "/", StringComparison.OrdinalIgnoreCase)
                ? entryUri.AbsolutePath[(endpointPath.Length + 1)..]
                : entryUri.AbsolutePath.Trim('/');
            entries.Add(new MirrorPulseWebDavRemoteEntry(
                Uri.UnescapeDataString(relative).Replace('\\', '/'),
                resourceType?.Element(Dav + "collection") is not null,
                lengthElement is not null && long.TryParse(lengthElement.Value, out var length) ? length : null,
                string.IsNullOrWhiteSpace(etag) ? null : etag));
        }

        return new ReadOnlyCollection<MirrorPulseWebDavRemoteEntry>(
            entries.OrderBy(entry => entry.RelativePath, StringComparer.Ordinal).ToArray());
    }
}

using System.Text.Json;

namespace MirrorPulse.Core.Packaging;

public sealed record OfficialReleaseAsset(string Name, Uri DownloadUri, long Size);

public sealed record LatestAdapterRelease(
    string TagName,
    string Name,
    Uri ReleasePageUri,
    IReadOnlyList<OfficialReleaseAsset> Assets,
    DateTimeOffset? PublishedAt);

/// <summary>
/// Parses the stable fields returned by the GitHub latest-release endpoint.
/// </summary>
public static class LatestAdapterReleaseParser
{
    public static LatestAdapterRelease Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var tagName = GetRequiredString(root, "tag_name");
        var name = root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString() ?? tagName
            : tagName;
        var releasePage = ParseHttpsUri(GetRequiredString(root, "html_url"), "html_url");
        var assets = new List<OfficialReleaseAsset>();
        if (root.TryGetProperty("assets", out var assetsElement) && assetsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsElement.EnumerateArray())
            {
                var assetName = GetRequiredString(asset, "name");
                var downloadUri = ParseHttpsUri(GetRequiredString(asset, "browser_download_url"), "browser_download_url");
                var size = asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0;
                assets.Add(new OfficialReleaseAsset(assetName, downloadUri, size));
            }
        }

        DateTimeOffset? publishedAt = null;
        if (root.TryGetProperty("published_at", out var publishedElement) &&
            publishedElement.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(publishedElement.GetString(), out var parsedPublishedAt))
        {
            publishedAt = parsedPublishedAt;
        }

        return new LatestAdapterRelease(tagName, name, releasePage, assets.AsReadOnly(), publishedAt);
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            throw new JsonException($"The latest release response must contain a non-empty '{propertyName}'.");
        }

        return property.GetString()!;
    }

    private static Uri ParseHttpsUri(string value, string propertyName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new JsonException($"The latest release '{propertyName}' must be an HTTPS URI.");
        }

        return uri;
    }
}

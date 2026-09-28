using System.Net.Http.Headers;

namespace MirrorPulse.Core.Adapters.WebDav;

public sealed class MirrorPulseWebDavConflictException : IOException
{
    public MirrorPulseWebDavConflictException(string relativePath, string? expectedETag, string? actualETag)
        : base($"The WebDAV ETag changed for '{relativePath}'.")
    {
        RelativePath = relativePath;
        ExpectedETag = expectedETag;
        ActualETag = actualETag;
    }

    public string RelativePath { get; }

    public string? ExpectedETag { get; }

    public string? ActualETag { get; }
}

/// <summary>
/// Applies and compares WebDAV entity tags for optimistic concurrency checks.
/// </summary>
public static class MirrorPulseWebDavEtagGuard
{
    public static void ApplyIfMatch(HttpRequestMessage request, string expectedETag)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        if (!EntityTagHeaderValue.TryParse(expectedETag, out var parsed))
        {
            throw new ArgumentException("The expected WebDAV ETag is invalid.", nameof(expectedETag));
        }

        request.Headers.IfMatch.Clear();
        request.Headers.IfMatch.Add(parsed);
    }

    public static void ApplyDestinationCondition(
        HttpRequestMessage request,
        Uri destination,
        string expectedETag)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
        if (!EntityTagHeaderValue.TryParse(expectedETag, out var parsed))
        {
            throw new ArgumentException("The expected WebDAV ETag is invalid.", nameof(expectedETag));
        }

        request.Headers.TryAddWithoutValidation("If", $"<{destination.AbsoluteUri}> ([{parsed}])");
    }

    public static bool Matches(string? expectedETag, string? actualETag) =>
        expectedETag is not null && actualETag is not null &&
        string.Equals(expectedETag, actualETag, StringComparison.Ordinal);
}

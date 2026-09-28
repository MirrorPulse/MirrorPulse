using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace MirrorPulse.Core.Adapters.WebDav;

public enum MirrorPulseWebDavAuthenticationKind
{
    None,
    Basic,
    Bearer,
}

/// <summary>
/// In-memory WebDAV credential material. MirrorPulse owns persistence and supplies only a live instance.
/// </summary>
public sealed class MirrorPulseWebDavCredential : IDisposable
{
    private byte[]? _secret;

    private MirrorPulseWebDavCredential(
        MirrorPulseWebDavAuthenticationKind kind,
        string? username,
        string secret)
    {
        if (kind == MirrorPulseWebDavAuthenticationKind.Basic)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(username);
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        Kind = kind;
        Username = username;
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public MirrorPulseWebDavAuthenticationKind Kind { get; }

    public string? Username { get; }

    public static MirrorPulseWebDavCredential CreateBasic(string username, string password) =>
        new(MirrorPulseWebDavAuthenticationKind.Basic, username, password);

    public static MirrorPulseWebDavCredential CreateBearer(string token) =>
        new(MirrorPulseWebDavAuthenticationKind.Bearer, null, token);

    internal string GetSecret()
    {
        ObjectDisposedException.ThrowIf(_secret is null, this);
        return Encoding.UTF8.GetString(_secret);
    }

    public override string ToString() => $"{Kind} WebDAV credential";

    public void Dispose()
    {
        if (_secret is not null)
        {
            CryptographicOperations.ZeroMemory(_secret);
            _secret = null;
        }
    }
}

/// <summary>
/// Applies a configured WebDAV authentication scheme to an HTTP request without performing network I/O.
/// </summary>
public static class MirrorPulseWebDavAuthenticator
{
    public static HttpRequestMessage CreateRequest(
        HttpMethod method,
        Uri endpoint,
        MirrorPulseWebDavCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("A WebDAV request endpoint must use HTTP or HTTPS.", nameof(endpoint));
        }

        var request = new HttpRequestMessage(method, endpoint);
        if (credential is not null)
        {
            Apply(request, credential);
        }

        return request;
    }

    public static void Apply(HttpRequestMessage request, MirrorPulseWebDavCredential credential)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(credential);
        request.Headers.Authorization = credential.Kind switch
        {
            MirrorPulseWebDavAuthenticationKind.None => null,
            MirrorPulseWebDavAuthenticationKind.Basic => new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.Username}:{credential.GetSecret()}"))),
            MirrorPulseWebDavAuthenticationKind.Bearer => new AuthenticationHeaderValue("Bearer", credential.GetSecret()),
            _ => throw new ArgumentOutOfRangeException(nameof(credential)),
        };
    }
}

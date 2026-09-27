using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

public sealed class OAuthDeviceCodeRequest
{
    public OAuthDeviceCodeRequest(string provider, string clientId, Uri deviceAuthorizationEndpoint, IEnumerable<string> scopes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(deviceAuthorizationEndpoint);
        if (!deviceAuthorizationEndpoint.IsAbsoluteUri || deviceAuthorizationEndpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The device authorization endpoint must be an absolute HTTPS URI.", nameof(deviceAuthorizationEndpoint));
        }

        ArgumentNullException.ThrowIfNull(scopes);
        var copiedScopes = scopes.Where(scope => !string.IsNullOrWhiteSpace(scope)).Select(scope => scope.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        if (copiedScopes.Length == 0)
        {
            throw new ArgumentException("At least one OAuth scope is required.", nameof(scopes));
        }

        Provider = provider.Trim();
        ClientId = clientId.Trim();
        DeviceAuthorizationEndpoint = deviceAuthorizationEndpoint;
        Scopes = new ReadOnlyCollection<string>(copiedScopes);
    }

    public string Provider { get; }

    public string ClientId { get; }

    public Uri DeviceAuthorizationEndpoint { get; }

    public IReadOnlyList<string> Scopes { get; }
}

/// <summary>
/// User-facing device-code instructions. The device code itself remains internal to the authorization session.
/// </summary>
public sealed class OAuthDeviceCodeChallenge
{
    public OAuthDeviceCodeChallenge(
        string sessionId,
        string userCode,
        Uri verificationUri,
        DateTimeOffset expiresAt,
        TimeSpan pollInterval,
        string deviceCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userCode);
        ArgumentNullException.ThrowIfNull(verificationUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceCode);
        if (!verificationUri.IsAbsoluteUri || verificationUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("The verification URI must be an absolute HTTPS URI.", nameof(verificationUri));
        }

        if (expiresAt <= DateTimeOffset.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAt), "A device-code challenge must not already be expired.");
        }

        if (pollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "The poll interval must be positive.");
        }

        SessionId = sessionId.Trim();
        UserCode = userCode.Trim();
        VerificationUri = verificationUri;
        ExpiresAt = expiresAt;
        PollInterval = pollInterval;
        DeviceCode = deviceCode;
    }

    public string SessionId { get; }

    public string UserCode { get; }

    public Uri VerificationUri { get; }

    public DateTimeOffset ExpiresAt { get; }

    public TimeSpan PollInterval { get; }

    internal string DeviceCode { get; }

    public override string ToString() => $"{SessionId}: {UserCode} at {VerificationUri}";
}

public enum OAuthDeviceCodePollStatus
{
    Pending,
    Authorized,
    SlowDown,
    Denied,
    Expired
}

public sealed record OAuthDeviceCodePollResult(
    OAuthDeviceCodePollStatus Status,
    CredentialReference? CredentialReference = null);

public interface IOAuthDeviceCodeAuthorizer
{
    ValueTask<OAuthDeviceCodeChallenge> BeginAsync(
        OAuthDeviceCodeRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<OAuthDeviceCodePollResult> PollAsync(
        OAuthDeviceCodeChallenge challenge,
        CancellationToken cancellationToken = default);
}

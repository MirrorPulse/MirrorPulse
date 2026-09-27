namespace MirrorPulse.Core.Contracts;

public enum AuthenticationUiStatus
{
    Idle,
    AwaitingUser,
    Polling,
    Authenticated,
    Failed,
    Expired,
    Cancelled
}

public enum AuthenticationFailureCode
{
    NetworkUnavailable,
    AccessDenied,
    ProviderRejected,
    InvalidConfiguration,
    Expired,
    Unknown
}

public sealed record AuthenticationFailure
{
    public AuthenticationFailure(AuthenticationFailureCode code, string messageKey, bool canRetry)
    {
        Code = code;
        MessageKey = ValidateMessageKey(messageKey);
        CanRetry = canRetry;
    }

    public AuthenticationFailureCode Code { get; }

    public string MessageKey { get; }

    public bool CanRetry { get; }

    private static string ValidateMessageKey(string messageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageKey);
        return messageKey.Trim();
    }
}

/// <summary>
/// UI-safe authentication state containing localization keys and no secret material.
/// </summary>
public sealed class AuthenticationUiState
{
    public AuthenticationUiState(
        AuthenticationUiStatus status,
        string provider,
        string? messageKey = null,
        AuthenticationFailure? failure = null,
        OAuthDeviceCodeChallenge? challenge = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        if (status == AuthenticationUiStatus.Failed && failure is null)
        {
            throw new ArgumentException("A failed authentication state must include a failure.", nameof(failure));
        }

        if (status != AuthenticationUiStatus.Failed && failure is not null)
        {
            throw new ArgumentException("Only failed authentication states may include a failure.", nameof(failure));
        }

        if (status == AuthenticationUiStatus.AwaitingUser && challenge is null)
        {
            throw new ArgumentException("An awaiting-user state must include a device-code challenge.", nameof(challenge));
        }

        if (status != AuthenticationUiStatus.AwaitingUser && challenge is not null)
        {
            throw new ArgumentException("Only awaiting-user states may include a device-code challenge.", nameof(challenge));
        }

        Status = status;
        Provider = provider.Trim();
        MessageKey = messageKey?.Trim();
        Failure = failure;
        Challenge = challenge;
    }

    public AuthenticationUiStatus Status { get; }

    public string Provider { get; }

    public string? MessageKey { get; }

    public AuthenticationFailure? Failure { get; }

    public OAuthDeviceCodeChallenge? Challenge { get; }
}

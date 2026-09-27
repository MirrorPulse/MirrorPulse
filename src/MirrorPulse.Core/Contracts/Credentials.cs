namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Secret material types that MP can reference in the current-user secure store.
/// </summary>
public enum CredentialKind
{
    Password,
    OAuthToken,
    ApiKey,
    SshKey,
    ClientCertificate,
    WindowsCredential,
    Custom
}

/// <summary>
/// Security boundary for a credential reference.
/// </summary>
public enum CredentialScope
{
    CurrentUser
}

/// <summary>
/// A non-secret pointer to material stored by MP in the current-user secure store.
/// </summary>
public sealed record CredentialReference
{
    public CredentialReference(
        string referenceId,
        CredentialKind kind,
        string provider,
        CredentialScope scope,
        DateTimeOffset createdAt,
        string? accountHint = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ReferenceId = referenceId;
        Kind = kind;
        Provider = provider;
        Scope = scope;
        CreatedAt = createdAt;
        AccountHint = accountHint;
    }

    public string ReferenceId { get; }

    public CredentialKind Kind { get; }

    public string Provider { get; }

    public CredentialScope Scope { get; }

    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Optional non-secret display hint such as an account name.
    /// </summary>
    public string? AccountHint { get; }
}

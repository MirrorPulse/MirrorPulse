using System.Security.Cryptography;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Current-user secure storage boundary for opaque credential bytes.
/// </summary>
public interface ISecureCredentialStore
{
    ValueTask SaveAsync(
        CredentialReference reference,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken = default);

    ValueTask<SecureCredentialValue?> TryGetAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Ephemeral secret bytes returned by a secure store. Dispose as soon as the caller finishes using them.
/// </summary>
public sealed class SecureCredentialValue : IDisposable
{
    private byte[] _value;
    private int _disposed;

    public SecureCredentialValue(CredentialReference reference, ReadOnlySpan<byte> value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        Reference = reference;
        _value = value.ToArray();
    }

    public CredentialReference Reference { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public ReadOnlyMemory<byte> Value => IsDisposed ? ReadOnlyMemory<byte>.Empty : _value;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            CryptographicOperations.ZeroMemory(_value);
            _value = Array.Empty<byte>();
        }
    }
}

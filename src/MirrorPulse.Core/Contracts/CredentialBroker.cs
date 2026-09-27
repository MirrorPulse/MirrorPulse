namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Request for a Worker-scoped credential lookup. It never contains secret material.
/// </summary>
public sealed record CredentialBrokerRequest
{
    public CredentialBrokerRequest(InstanceId instanceId, CredentialReference reference, string purpose)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        InstanceId = instanceId;
        Reference = reference;
        Purpose = purpose.Trim();
    }

    public InstanceId InstanceId { get; }

    public CredentialReference Reference { get; }

    public string Purpose { get; }
}

public interface ICredentialBroker
{
    ValueTask<CredentialLease> ResolveAsync(CredentialBrokerRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Ephemeral credential material that is cleared when the Worker finishes using it.
/// </summary>
public sealed class CredentialLease : IDisposable
{
    private char[] _value;
    private int _disposed;

    public CredentialLease(CredentialReference reference, string value)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(value);
        Reference = reference;
        _value = value.ToCharArray();
    }

    public CredentialReference Reference { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public ReadOnlyMemory<char> Value => IsDisposed ? ReadOnlyMemory<char>.Empty : _value;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            Array.Clear(_value);
            _value = Array.Empty<char>();
        }
    }
}

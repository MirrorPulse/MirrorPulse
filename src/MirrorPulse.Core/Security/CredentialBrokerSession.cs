using System.Security.Cryptography;
using System.Text;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Security;

/// <summary>
/// Limits credential resolution to one Adapter instance and one current-user broker session.
/// </summary>
public sealed class CredentialBrokerSession : ICredentialBroker, IDisposable
{
    private readonly ISecureCredentialStore _store;
    private int _disposed;

    public CredentialBrokerSession(InstanceId instanceId, ISecureCredentialStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        InstanceId = instanceId;
        SessionId = Guid.NewGuid();
        _store = store;
    }

    public Guid SessionId { get; }

    public InstanceId InstanceId { get; }

    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    public async ValueTask<CredentialLease> ResolveAsync(
        CredentialBrokerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureOpen();
        if (request.InstanceId != InstanceId)
        {
            throw new UnauthorizedAccessException("The credential request belongs to another Adapter instance.");
        }

        var stored = await _store.TryGetAsync(request.Reference, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException("The requested credential was not found.");
        using (stored)
        {
            var bytes = stored.Value.ToArray();
            var chars = Encoding.UTF8.GetChars(bytes);
            try
            {
                return new CredentialLease(request.Reference, chars);
            }
            finally
            {
                Array.Clear(chars);
                CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);

    private void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
    }
}

using System.Text;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

internal sealed class FixedCredentialStore(string referenceId, string secret) : ISecureCredentialStore
{
    public ValueTask SaveAsync(CredentialReference reference, ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new NotSupportedException("This fixture does not persist credentials."));

    public ValueTask<SecureCredentialValue?> TryGetAsync(CredentialReference reference,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<SecureCredentialValue?>(reference.ReferenceId == referenceId
            ? new SecureCredentialValue(reference, Encoding.UTF8.GetBytes(secret))
            : null);

    public ValueTask DeleteAsync(CredentialReference reference,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromException(new NotSupportedException("This fixture does not persist credentials."));
}

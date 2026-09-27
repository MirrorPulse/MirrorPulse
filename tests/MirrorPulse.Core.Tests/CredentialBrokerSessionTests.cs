using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Security;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CredentialBrokerSessionTests
{
    [TestMethod]
    public async Task SessionResolvesOnlyItsInstanceCredential()
    {
        var instanceId = InstanceId.New();
        var reference = new CredentialReference("ref-1", CredentialKind.Password, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        var store = new FakeStore(reference, "secret"u8.ToArray());
        using var session = new CredentialBrokerSession(instanceId, store);

        using var lease = await session.ResolveAsync(new CredentialBrokerRequest(instanceId, reference, "sync"));

        Assert.AreEqual("secret", lease.Value.ToString());
        await Assert.ThrowsExactlyAsync<UnauthorizedAccessException>(() => session.ResolveAsync(
            new CredentialBrokerRequest(InstanceId.New(), reference, "sync")).AsTask());
    }

    [TestMethod]
    public async Task DisposedSessionRejectsNewRequests()
    {
        var instanceId = InstanceId.New();
        var reference = new CredentialReference("ref-1", CredentialKind.Password, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        var session = new CredentialBrokerSession(instanceId, new FakeStore(reference, "secret"u8.ToArray()));
        session.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => session.ResolveAsync(
            new CredentialBrokerRequest(instanceId, reference, "sync")).AsTask());
    }

    private sealed class FakeStore(CredentialReference reference, byte[] secret) : ISecureCredentialStore
    {
        public ValueTask SaveAsync(CredentialReference requested, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public ValueTask<SecureCredentialValue?> TryGetAsync(CredentialReference requested, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(requested == reference ? new SecureCredentialValue(reference, secret) : null);
        }

        public ValueTask DeleteAsync(CredentialReference requested, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}

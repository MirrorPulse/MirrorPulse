using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SecureCredentialStoreContractTests
{
    [TestMethod]
    public void SecureValueCopiesBytesAndClearsThemOnDispose()
    {
        var reference = new CredentialReference("ref-1", CredentialKind.ApiKey, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        var source = new byte[] { 1, 2, 3 };
        using var value = new SecureCredentialValue(reference, source);
        source[0] = 9;

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, value.Value.ToArray());
        value.Dispose();

        Assert.IsTrue(value.IsDisposed);
        Assert.IsTrue(value.Value.IsEmpty);
    }

    [TestMethod]
    public async Task SecureStoreContractKeepsReferencesCurrentUserScoped()
    {
        var reference = new CredentialReference("ref-1", CredentialKind.Password, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        var store = new InMemorySecureCredentialStore();
        await store.SaveAsync(reference, "secret"u8.ToArray());

        using var value = await store.TryGetAsync(reference);

        Assert.IsNotNull(value);
        Assert.AreEqual(reference, value.Reference);
        CollectionAssert.AreEqual("secret"u8.ToArray(), value.Value.ToArray());
    }

    private sealed class InMemorySecureCredentialStore : ISecureCredentialStore
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public ValueTask SaveAsync(CredentialReference reference, ReadOnlyMemory<byte> secret, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(CredentialScope.CurrentUser, reference.Scope);
            _values[reference.ReferenceId] = secret.ToArray();
            return ValueTask.CompletedTask;
        }

        public ValueTask<SecureCredentialValue?> TryGetAsync(CredentialReference reference, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(CredentialScope.CurrentUser, reference.Scope);
            return ValueTask.FromResult(_values.TryGetValue(reference.ReferenceId, out var secret)
                ? new SecureCredentialValue(reference, secret)
                : null);
        }

        public ValueTask DeleteAsync(CredentialReference reference, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(CredentialScope.CurrentUser, reference.Scope);
            _values.Remove(reference.ReferenceId);
            return ValueTask.CompletedTask;
        }
    }
}

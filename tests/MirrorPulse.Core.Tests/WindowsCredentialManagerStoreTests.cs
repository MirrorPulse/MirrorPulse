using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Security;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WindowsCredentialManagerStoreTests
{
    [TestMethod]
    public async Task StoreUsesStableCurrentUserTargetAndSupportsRoundTrip()
    {
        var api = new FakeCredentialManagerApi();
        var store = new WindowsCredentialManagerStore(api);
        var reference = new CredentialReference("install-1", CredentialKind.ApiKey, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);

        await store.SaveAsync(reference, "secret"u8.ToArray());
        using var value = await store.TryGetAsync(reference);

        Assert.AreEqual("MirrorPulse/install-1", WindowsCredentialManagerStore.GetTargetName(reference));
        Assert.IsNotNull(value);
        CollectionAssert.AreEqual("secret"u8.ToArray(), value.Value.ToArray());
        await store.DeleteAsync(reference);
        Assert.IsFalse(api.Contains("MirrorPulse/install-1"));
    }

    [TestMethod]
    public async Task StoreRejectsNonCurrentUserReferences()
    {
        var reference = new CredentialReference("install-1", CredentialKind.ApiKey, "example", (CredentialScope)99, DateTimeOffset.UtcNow);
        var api = new FakeCredentialManagerApi();
        var store = new WindowsCredentialManagerStore(api);

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => store.SaveAsync(reference, "secret"u8.ToArray()).AsTask());
    }

    private sealed class FakeCredentialManagerApi : IWindowsCredentialManagerApi
    {
        private readonly Dictionary<string, byte[]> _values = new(StringComparer.Ordinal);

        public void Write(string targetName, ReadOnlyMemory<byte> secret) => _values[targetName] = secret.ToArray();

        public bool TryRead(string targetName, out byte[] secret)
        {
            if (_values.TryGetValue(targetName, out var value))
            {
                secret = value.ToArray();
                return true;
            }

            secret = Array.Empty<byte>();
            return false;
        }

        public void Delete(string targetName) => _values.Remove(targetName);

        public bool Contains(string targetName) => _values.ContainsKey(targetName);
    }
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CredentialBrokerTests
{
    [TestMethod]
    public async Task BrokerRequestCarriesReferenceWithoutSecretAndLeaseClearsValue()
    {
        var reference = new CredentialReference("ref-1", CredentialKind.Password, "Windows", CredentialScope.CurrentUser, DateTimeOffset.UtcNow, "user");
        var request = new CredentialBrokerRequest(InstanceId.New(), reference, "connect");
        var broker = new FakeCredentialBroker(reference, "temporary-secret");

        using var lease = await broker.ResolveAsync(request);

        Assert.AreEqual(reference, lease.Reference);
        Assert.AreEqual("temporary-secret", lease.Value.ToString());
        Assert.IsFalse(request.ToString()!.Contains("temporary-secret", StringComparison.Ordinal));
        lease.Dispose();
        Assert.IsTrue(lease.IsDisposed);
        Assert.IsTrue(lease.Value.IsEmpty);
    }

    private sealed class FakeCredentialBroker(CredentialReference reference, string value) : ICredentialBroker
    {
        public ValueTask<CredentialLease> ResolveAsync(CredentialBrokerRequest request, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual(reference, request.Reference);
            return ValueTask.FromResult(new CredentialLease(reference, value));
        }
    }
}

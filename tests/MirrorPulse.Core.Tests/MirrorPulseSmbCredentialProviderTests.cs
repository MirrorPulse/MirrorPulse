using MirrorPulse.Core.Adapters.Smb;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSmbCredentialProviderTests
{
    [TestMethod]
    public async Task ProviderResolvesCurrentUserCredentialForItsInstance()
    {
        var instanceId = InstanceId.New();
        var reference = new CredentialReference(
            "smb-credential",
            CredentialKind.WindowsCredential,
            "SMB",
            CredentialScope.CurrentUser,
            DateTimeOffset.UtcNow,
            "alice");
        var broker = new RecordingBroker();
        var provider = new MirrorPulseSmbCredentialProvider(instanceId, broker);

        var (binding, lease) = await provider.ResolveAsync("\\\\server\\share", reference);
        using (lease)
        {
            Assert.AreEqual(instanceId, broker.LastRequest!.InstanceId);
            Assert.AreEqual("smb.connect", broker.LastRequest.Purpose);
            Assert.AreEqual("alice", binding.AccountName);
            Assert.IsFalse(binding.UsesCurrentWindowsIdentity);
            Assert.AreEqual("secret", lease.Value.ToString());
        }

        Assert.IsTrue(lease.IsDisposed);
    }

    [TestMethod]
    public void ProviderAllowsIntegratedWindowsIdentityWithoutSecret()
    {
        var binding = MirrorPulseSmbCredentialProvider.UseCurrentIdentity("\\\\server\\share");

        Assert.IsTrue(binding.UsesCurrentWindowsIdentity);
        Assert.IsNull(binding.Reference);
    }

    private sealed class RecordingBroker : ICredentialBroker
    {
        public CredentialBrokerRequest? LastRequest { get; private set; }

        public ValueTask<CredentialLease> ResolveAsync(
            CredentialBrokerRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return ValueTask.FromResult(new CredentialLease(request.Reference, "secret"));
        }
    }
}

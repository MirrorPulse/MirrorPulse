using System.Net.Http.Headers;
using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavAuthenticatorTests
{
    [TestMethod]
    public void AuthenticatorAppliesBasicAndBearerHeaders()
    {
        using var basic = MirrorPulseWebDavCredential.CreateBasic("alice", "secret");
        using var bearer = MirrorPulseWebDavCredential.CreateBearer("token-value");
        using var basicRequest = MirrorPulseWebDavAuthenticator.CreateRequest(
            HttpMethod.Options,
            new Uri("https://dav.example.test/"),
            basic);
        using var bearerRequest = MirrorPulseWebDavAuthenticator.CreateRequest(
            HttpMethod.Options,
            new Uri("https://dav.example.test/"),
            bearer);

        Assert.AreEqual("Basic YWxpY2U6c2VjcmV0", basicRequest.Headers.Authorization?.ToString());
        Assert.AreEqual("Bearer token-value", bearerRequest.Headers.Authorization?.ToString());
        Assert.AreEqual("Basic WebDAV credential", basic.ToString());
    }

    [TestMethod]
    public void DisposedCredentialsCannotBeAppliedAndUnsupportedEndpointsAreRejected()
    {
        var credential = MirrorPulseWebDavCredential.CreateBearer("token-value");
        credential.Dispose();
        using var request = new HttpRequestMessage(HttpMethod.Options, "https://dav.example.test/");
        Assert.ThrowsExactly<ObjectDisposedException>(() => MirrorPulseWebDavAuthenticator.Apply(request, credential));
        Assert.ThrowsExactly<ArgumentException>(() => MirrorPulseWebDavAuthenticator.CreateRequest(
            HttpMethod.Options,
            new Uri("file:///tmp/dav"),
            null));
    }
}

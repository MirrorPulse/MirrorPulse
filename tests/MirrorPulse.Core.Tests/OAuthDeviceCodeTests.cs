using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class OAuthDeviceCodeTests
{
    [TestMethod]
    public async Task DeviceCodeContractKeepsSecretOutOfUserFacingText()
    {
        var request = new OAuthDeviceCodeRequest(
            "example",
            "client-1",
            new Uri("https://login.example.test/device"),
            ["files.read", "files.read"]);
        var authorizer = new FakeAuthorizer();

        var challenge = await authorizer.BeginAsync(request);
        var result = await authorizer.PollAsync(challenge);

        Assert.HasCount(1, request.Scopes);
        Assert.AreEqual(OAuthDeviceCodePollStatus.Authorized, result.Status);
        Assert.IsFalse(challenge.ToString().Contains("device-secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DeviceCodeContractRejectsInsecureEndpoints()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new OAuthDeviceCodeRequest(
            "example",
            "client-1",
            new Uri("http://login.example.test/device"),
            ["files.read"]));
    }

    private sealed class FakeAuthorizer : IOAuthDeviceCodeAuthorizer
    {
        public ValueTask<OAuthDeviceCodeChallenge> BeginAsync(OAuthDeviceCodeRequest request, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new OAuthDeviceCodeChallenge(
                "session-1",
                "ABCD-EFGH",
                new Uri("https://login.example.test/activate"),
                DateTimeOffset.UtcNow.AddMinutes(5),
                TimeSpan.FromSeconds(5),
                "device-secret"));
        }

        public ValueTask<OAuthDeviceCodePollResult> PollAsync(OAuthDeviceCodeChallenge challenge, CancellationToken cancellationToken = default)
        {
            var reference = new CredentialReference(
                "oauth-ref",
                CredentialKind.OAuthToken,
                "example",
                CredentialScope.CurrentUser,
                DateTimeOffset.UtcNow);
            return ValueTask.FromResult(new OAuthDeviceCodePollResult(OAuthDeviceCodePollStatus.Authorized, reference));
        }
    }
}

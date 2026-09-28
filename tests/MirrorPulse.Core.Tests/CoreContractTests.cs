using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CoreContractTests
{
    private static readonly int ExpectedSchemaVersion = 1;

    [TestMethod]
    public void CurrentSchemaAndFrameContractsRemainCompatible()
    {
        Assert.AreEqual(MirrorPulseConfiguration.CurrentSchemaVersion, ExpectedSchemaVersion);
        Assert.AreEqual(4L + 1024L, ControlFrameLimits.GetFrameLength(1024));

        using var document = JsonDocument.Parse("{\"kind\":\"test\"}");
        var envelope = new ControlFrameEnvelope(
            1,
            "Test",
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            isResponse: false,
            document.RootElement);

        Assert.AreEqual(1, envelope.ProtocolVersion);
        Assert.AreEqual("test", envelope.Payload.GetProperty("kind").GetString());
    }

    [TestMethod]
    public void HandshakeAndMessageContractsPreserveCorrelation()
    {
        var instanceId = InstanceId.New();
        var sessionId = WorkerSessionId.New();
        var requestId = Guid.NewGuid();
        var offer = new WorkerProtocolOffer(
            AdapterId.Parse("example.local"),
            "1.0.0",
            sessionId,
            "win-x64",
            new ProtocolVersionRange(1, 1),
            Sha256Digest.Parse(new string('F', 64)));
        var hello = new HelloMessage(requestId, instanceId, offer);
        var ready = new ReadyMessage(
            requestId,
            instanceId,
            sessionId,
            new WorkerProtocolSelection(true, 1),
            Array.Empty<RootRegistration>());
        var heartbeat = new HeartbeatMessage(requestId, instanceId, sessionId, 1, DateTimeOffset.UtcNow);
        var health = new HealthMessage(requestId, instanceId, sessionId, HealthStatus.Healthy, 1, DateTimeOffset.UtcNow);

        Assert.AreEqual(hello.RequestId, ready.RequestId);
        Assert.AreEqual(heartbeat.RequestId, health.RequestId);
        Assert.AreEqual(hello.WorkerSessionId, health.WorkerSessionId);
        Assert.IsTrue(ready.Selection.Accepted);
    }

    [TestMethod]
    public void SecurityContractsKeepCredentialsOutOfLogsAndConfiguration()
    {
        var field = new LogField("refresh_token", "secret");
        var credential = new CredentialReference(
            "credential-1",
            CredentialKind.OAuthToken,
            "example.local",
            CredentialScope.CurrentUser,
            DateTimeOffset.UtcNow);
        var configuration = new MirrorPulseConfiguration(
            1,
            "en-US",
            developerMode: false,
            startWithWindows: false,
            enabledInstallations: [InstallId.New()]);

        Assert.AreEqual(LogFieldPolicy.RedactedValue, field.Value);
        Assert.AreEqual(CredentialScope.CurrentUser, credential.Scope);
        Assert.IsFalse(configuration.DeveloperMode);
        Assert.IsNull(typeof(CredentialReference).GetProperty("Secret"));
    }
}

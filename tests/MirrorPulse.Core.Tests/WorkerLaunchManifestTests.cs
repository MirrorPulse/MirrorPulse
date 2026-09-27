using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerLaunchManifestTests
{
    private static readonly string[] CredentialReferences = ["credential-123"];

    [TestMethod]
    public void ManifestContainsOnlyNonSecretStartupMaterial()
    {
        var instance = new AdapterInstance(
            new AdapterId("sample.adapter"),
            InstallId.New(),
            InstanceId.New(),
            "Sample",
            new Dictionary<string, string> { ["endpoint"] = "https://example.invalid" },
            CredentialReferences,
            "C:\\MirrorPulse\\cache\\files",
            "C:\\MirrorPulse\\cache\\transfers",
            true,
            AdapterLifecycleState.Enabled,
            null,
            DateTimeOffset.UtcNow);
        var session = WorkerSessionId.New();

        var manifest = WorkerLaunchManifest.Create(instance, session, Environment.ProcessPath!, AppContext.BaseDirectory, "mirrorpulse-test-pipe");
        var request = manifest.ToRequest();

        Assert.AreEqual(instance.InstanceId.ToString(), manifest.Environment["MP_INSTANCE_ID"]);
        Assert.AreEqual(session.ToString(), manifest.Environment["MP_WORKER_SESSION_ID"]);
        Assert.AreEqual("credential-123", manifest.Environment["MP_CREDENTIAL_REFERENCES"]);
        Assert.IsTrue(manifest.Arguments.Contains("--pipe-name"));
        Assert.AreEqual(manifest.ExecutablePath, request.ExecutablePath);
        Assert.AreEqual(manifest.Environment["MP_PIPE_NAME"], request.Environment["MP_PIPE_NAME"]);
        Assert.AreEqual(manifest.Environment["MP_FILE_CACHE_DIR"], request.Environment["MP_FILE_CACHE_DIR"]);
    }
}

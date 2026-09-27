using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstanceTests
{
    [TestMethod]
    public void InstancesFromOneInstallationRemainIndependent()
    {
        var installId = InstallId.New();
        var first = CreateInstance(installId, InstanceId.New(), "First");
        var second = CreateInstance(installId, InstanceId.New(), "Second");

        Assert.AreEqual(first.AdapterId, second.AdapterId);
        Assert.AreEqual(first.InstallId, second.InstallId);
        Assert.AreNotEqual(first.InstanceId, second.InstanceId);
        Assert.AreNotEqual(first.FileCacheDirectory, second.FileCacheDirectory);
    }

    [TestMethod]
    public void InstanceCopiesConfigurationAndCredentialReferences()
    {
        var configuration = new Dictionary<string, string> { ["endpoint"] = "https://example.test" };
        var credentials = new List<string> { "credential-1" };
        var instance = new AdapterInstance(
            AdapterId.Parse("example.webdav"),
            InstallId.New(),
            InstanceId.New(),
            "Example",
            configuration,
            credentials,
            "C:\\MirrorPulse\\cache\\files\\instance",
            "C:\\MirrorPulse\\cache\\transfers\\instance",
            enabled: true,
            lifecycleState: AdapterLifecycleState.Configured,
            workerSessionId: null,
            createdAt: new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero));

        configuration["endpoint"] = "https://changed.test";
        credentials.Add("credential-2");

        Assert.AreEqual("https://example.test", instance.Configuration["endpoint"]);
        Assert.HasCount(1, instance.CredentialReferences);
        Assert.IsTrue(instance.Enabled);
        Assert.IsNull(instance.WorkerSessionId);
    }

    private static AdapterInstance CreateInstance(InstallId installId, InstanceId instanceId, string name) => new(
        AdapterId.Parse("example.webdav"),
        installId,
        instanceId,
        name,
        new Dictionary<string, string> { ["endpoint"] = "https://example.test" },
        ["credential-1"],
        $"C:\\MirrorPulse\\cache\\files\\{instanceId}",
        $"C:\\MirrorPulse\\cache\\transfers\\{instanceId}",
        enabled: true,
        lifecycleState: AdapterLifecycleState.Enabled,
        workerSessionId: WorkerSessionId.New(),
        createdAt: new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero));
}

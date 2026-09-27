using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstallationHealthCheckerTests
{
    [TestMethod]
    public async Task HealthCheckAcceptsAnInstallationWithItsRuntimeEntrypoint()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-health-{Guid.NewGuid():N}");
        try
        {
            var directory = Path.Combine(root, "installed");
            Directory.CreateDirectory(Path.Combine(directory, "payload", "win-x64"));
            await File.WriteAllTextAsync(Path.Combine(directory, "payload", "win-x64", "Adapter.exe"), "worker");
            var installed = CreateInstalledAdapter(directory);

            var health = AdapterInstallationHealthChecker.Check(installed, "win-x64");

            Assert.IsTrue(health.IsHealthy);
            Assert.IsEmpty(health.Problems);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [TestMethod]
    public void HealthCheckReportsMissingEntrypointWithoutExecutingThePayload()
    {
        var installed = CreateInstalledAdapter(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        var health = AdapterInstallationHealthChecker.Check(installed, "win-x64");

        Assert.IsFalse(health.IsHealthy);
        CollectionAssert.Contains(health.Problems.ToArray(), "installation.directory.missing");
        CollectionAssert.Contains(health.Problems.ToArray(), "installation.entrypoint.fileMissing");
    }

    private static InstalledAdapter CreateInstalledAdapter(string directory) => new(
        new AdapterManifest(
            1,
            AdapterId.Parse("example.webdav"),
            "Example Publisher",
            "1.0.0",
            new ProtocolVersionRange(1, 1),
            new Dictionary<string, string>
            {
                ["win-x64"] = "payload/win-x64/Adapter.exe",
                ["win-arm64"] = "payload/win-arm64/Adapter.exe"
            },
            new AdapterInstallPolicy(null),
            new AdapterInstancePolicy(null, null),
            new AdapterCapabilities(true, true, true, true),
            ["en-US"],
            "1.0.0"),
        InstallId.New(),
        directory,
        Sha256Digest.Parse(new string('A', 64)),
        AdapterInstallSource.LocalFile,
        null,
        isSigned: true,
        DateTimeOffset.UtcNow,
        AdapterLifecycleState.Installed);
}

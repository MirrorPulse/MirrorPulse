using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterInstanceWorkerLaunchResolverTests
{
    [TestMethod]
    public async Task TwoInstancesOfSameAdapterPinDifferentPayloadVersions()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        AdapterId adapterId = AdapterId.Parse("example.drive");
        var oldInstallId = InstallId.New();
        var newInstallId = InstallId.New();
        var oldInstanceId = InstanceId.New();
        var newInstanceId = InstanceId.New();
        string entrypoint = "worker/win-x64/Adapter.exe";
        try
        {
            InstalledAdapter oldInstallation = await CreateInstallationAsync(oldInstallId, "1.0.0", "old");
            InstalledAdapter newInstallation = await CreateInstallationAsync(newInstallId, "2.0.0", "new");
            var topology = new MirrorPulseAdapterTopology([oldInstallation, newInstallation],
                [CreateInstance(oldInstallId, oldInstanceId), CreateInstance(newInstallId, newInstanceId)], []);

            AdapterInstanceWorkerPayload oldPayload = AdapterInstanceWorkerLaunchResolver.Resolve(
                topology, oldInstanceId, WorkerSessionId.New(), "win-x64");
            AdapterInstanceWorkerPayload newPayload = AdapterInstanceWorkerLaunchResolver.Resolve(
                topology, newInstanceId, WorkerSessionId.New(), "win-x64");
            Assert.AreEqual(oldInstallId, oldPayload.InstallId);
            Assert.AreEqual(newInstallId, newPayload.InstallId);
            Assert.AreEqual("1.0.0", oldPayload.Version);
            Assert.AreEqual("2.0.0", newPayload.Version);
            Assert.AreEqual("old", await File.ReadAllTextAsync(oldPayload.LaunchRequest.ExecutablePath));
            Assert.AreEqual("new", await File.ReadAllTextAsync(newPayload.LaunchRequest.ExecutablePath));
            Assert.AreEqual(Path.Combine(newInstallation.InstallationDirectory,
                entrypoint.Replace('/', Path.DirectorySeparatorChar)), newPayload.LaunchRequest.ExecutablePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }

        async Task<InstalledAdapter> CreateInstallationAsync(InstallId id, string version, string content)
        {
            string directory = Path.Combine(root, id.ToString());
            string executable = Path.Combine(directory, entrypoint.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            await File.WriteAllTextAsync(executable, content);
            var manifest = new AdapterManifest(1, adapterId, "Example", version,
                new ProtocolVersionRange(1, 1), new Dictionary<string, string>
                {
                    ["win-x64"] = entrypoint,
                    ["win-arm64"] = "worker/win-arm64/Adapter.exe",
                }, new AdapterInstallPolicy(null), new AdapterInstancePolicy(null, null),
                new AdapterCapabilities(true, false, true, true), ["en-US"], "1.0.0");
            return new InstalledAdapter(manifest, id, directory, new Sha256Digest(new string('A', 64)),
                AdapterInstallSource.LocalFile, null, true, DateTimeOffset.UtcNow,
                AdapterLifecycleState.Installed);
        }

        AdapterInstance CreateInstance(InstallId installId, InstanceId instanceId) =>
            new(adapterId, installId, instanceId, "Drive", new Dictionary<string, string>(), [],
                Path.Combine(root, "file-cache"), Path.Combine(root, "transfer-cache"), true,
                AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
    }
}

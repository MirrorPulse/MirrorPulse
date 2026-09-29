using System.Text;
using System.Text.Json;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Packaging;
using MirrorPulse.Core.Security;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class OfficialAdapterAggregateProcessTests
{
    [TestMethod]
    public async Task LatestOfficialReleasesInstallWithTheBuiltInTrustAnchor()
    {
        string? aggregateDirectory = Environment.GetEnvironmentVariable("MIRRORPULSE_OFFICIAL_AGGREGATE");
        if (string.IsNullOrWhiteSpace(aggregateDirectory))
        {
            return;
        }

        string manifestPath = Path.Combine(aggregateDirectory, "official-adapters.manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        JsonElement.ArrayEnumerator adapters = manifest.RootElement.EnumerateArray();
        string installationRoot = Path.Combine(Path.GetTempPath(),
            $"MirrorPulse-official-aggregate-{Guid.NewGuid():N}");
        try
        {
            using var trustedKey = MirrorPulseOfficialAdapterTrust.CreatePublicKey();
            int count = 0;
            foreach (JsonElement entry in adapters)
            {
                string adapterId = entry.GetProperty("adapterId").GetString()!;
                string version = entry.GetProperty("version").GetString()!;
                string packageName = entry.GetProperty("package").GetString()!;
                string signatureName = entry.GetProperty("signature").GetString()!;
                string directory = Path.Combine(aggregateDirectory, adapterId);
                SignedProcessAdapterInstallation installed = await SignedProcessAdapterInstaller.InstallAsync(
                    Path.Combine(directory, packageName), Path.Combine(directory, signatureName),
                    installationRoot, "win-x64", trustedKey, MirrorPulseOfficialAdapterTrust.Signer);
                Assert.AreEqual(adapterId, installed.AdapterId);
                Assert.AreEqual(version, installed.Version);
                Assert.IsTrue(File.Exists(installed.ExecutablePath));
                count++;
            }

            Assert.AreEqual(5, count);
        }
        finally
        {
            if (Directory.Exists(installationRoot))
            {
                Directory.Delete(installationRoot, recursive: true);
            }
        }
    }

    [TestMethod]
    public async Task SignedLocalReleaseListsHydratesAndUploadsThroughHostAndCfSharp()
    {
        string? aggregateDirectory = Environment.GetEnvironmentVariable("MIRRORPULSE_OFFICIAL_AGGREGATE");
        if (string.IsNullOrWhiteSpace(aggregateDirectory))
        {
            return;
        }

        using JsonDocument manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(aggregateDirectory, "official-adapters.manifest.json")));
        JsonElement local = manifest.RootElement.EnumerateArray().Single(item =>
            item.GetProperty("adapterId").GetString() == "com.mirrorpulse.adapter.local");
        string packageDirectory = Path.Combine(aggregateDirectory, "com.mirrorpulse.adapter.local");
        string packagePath = Path.Combine(packageDirectory, local.GetProperty("package").GetString()!);
        string signaturePath = Path.Combine(packageDirectory, local.GetProperty("signature").GetString()!);
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-official-local", Guid.NewGuid().ToString("N"));
        string sourceDirectory = Path.Combine(root, "source");
        string syncRoot = Path.Combine(root, "sync");
        var paths = new MirrorPulseStoragePaths(syncRoot, Path.Combine(root, "data"));
        byte[] expected = Encoding.UTF8.GetBytes("signed-local-adapter-content");
        try
        {
            Directory.CreateDirectory(sourceDirectory);
            await File.WriteAllBytesAsync(Path.Combine(sourceDirectory, "note.txt"), expected);
            await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
            InstalledAdapter installation = await catalog.InstallSignedAdapterAsync(packagePath,
                signaturePath, Path.Combine(root, "installed"), "win-x64");
            AdapterInstance instance = await catalog.CreateInstanceAsync(installation.InstallId,
                "Local source", new Dictionary<string, string> { ["sourceDirectory"] = sourceDirectory },
                [], Path.Combine(root, "cache", "files"), Path.Combine(root, "cache", "transfers"));
            MirrorPulseAdapterTopology topology = await catalog.ReadAdapterTopologyAsync();
            Assert.HasCount(1, topology.Roots);
            RootRegistration registration = topology.Roots.Single();
            await using var supervisor = new AdapterInstanceProcessSupervisor(catalog,
                new WindowsCredentialManagerStore());
            await supervisor.StartAsync(topology);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (true)
            {
                MirrorPulseInstanceRuntimeState? state = await catalog.ReadInstanceRuntimeStateAsync(
                    instance.InstanceId, timeout.Token);
                if (state?.Phase == "Connected")
                {
                    break;
                }

                if (state?.Phase is "Worker failed" or "Worker error")
                {
                    Assert.Fail($"The signed Local Worker failed: {state.LastErrorCode}.");
                }

                await Task.Delay(50, timeout.Token);
            }

            var router = new MirrorPulseRootRouter(syncRoot, topology.Roots);
            var provider = new MirrorPulseDemandProvider(router, supervisor,
                new MirrorPulseAdapterDirectoryPageSource(supervisor));
            CloudProviderDirectoryPage top = await provider.FetchChildrenAsync(syncRoot,
                ReadOnlyMemory<byte>.Empty, null, timeout.Token);
            Assert.HasCount(1, top.Children);
            CloudPlaceholderSpec adapterRoot = top.Children.Single();
            Assert.AreEqual(registration.DirectoryName, adapterRoot.Name);
            CloudProviderDirectoryPage page = await provider.FetchChildrenAsync(
                Path.Combine(syncRoot, registration.DirectoryName),
                adapterRoot.Identity.Encode(), null, timeout.Token);
            CloudFilePlaceholderSpec note = page.Children.OfType<CloudFilePlaceholderSpec>()
                .Single(item => item.Name == "note.txt");
            Assert.AreEqual(expected.LongLength, note.Length);

            await using Stream read = await provider.OpenReadAsync(
                Path.Combine(syncRoot, registration.DirectoryName, "note.txt"),
                note.Identity.Encode(), note.Length, 0, note.Length, timeout.Token);
            byte[] actual = new byte[expected.Length];
            await read.ReadExactlyAsync(actual, timeout.Token);
            CollectionAssert.AreEqual(expected, actual);

            byte[] upload = Encoding.UTF8.GetBytes("uploaded-through-signed-worker");
            await using var content = new MemoryStream(upload, writable: false);
            string revision = await supervisor.UploadAsync(new MirrorPulseWorkerUploadRequest(
                instance.InstanceId, "uploaded.txt", null, content, upload.Length), timeout.Token);
            Assert.IsFalse(string.IsNullOrWhiteSpace(revision));
            CollectionAssert.AreEqual(upload, await File.ReadAllBytesAsync(
                Path.Combine(sourceDirectory, "uploaded.txt"), timeout.Token));
            Assert.AreEqual(revision, await supervisor.StatAsync(new MirrorPulseWorkerStatRequest(
                instance.InstanceId, "uploaded.txt"), timeout.Token));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

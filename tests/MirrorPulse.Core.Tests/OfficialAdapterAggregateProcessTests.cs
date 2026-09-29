using System.Text.Json;
using MirrorPulse.Core.Packaging;
using MirrorPulse.Core.Security;

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
}

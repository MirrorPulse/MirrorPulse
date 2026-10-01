using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CiEvidenceGateTests
{
    [TestMethod]
    [DataRow("valid", true)]
    [DataRow("changed", false)]
    [DataRow("traversal", false)]
    [DataRow("installed-valid", true)]
    [DataRow("installed-server", false)]
    [DataRow("installed-old-build", false)]
    public async Task EvidenceVerifiesArtifactBytesAndRejectsUnsafePaths(string scenario, bool succeeds)
    {
        string repository = SftpProtocolFixture.FindRepositoryRoot();
        string root = Path.Combine(Path.GetTempPath(), $"MirrorPulse-evidence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "host"));
        try
        {
            byte[] bytes = "fixture-payload"u8.ToArray();
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await File.WriteAllBytesAsync(Path.Combine(root, "mp.exe"), bytes);
            await File.WriteAllBytesAsync(Path.Combine(root, "host", "MirrorPulse.Host.exe"), bytes);
            string cliPath = scenario == "traversal" ? "../mp.exe" : "mp.exe";
            var publish = new
            {
                schemaVersion = 1,
                runtime = "win-x64",
                files = new[] { new { path = cliPath, sha256 = hash, length = bytes.Length },
                    new { path = "host/MirrorPulse.Host.exe", sha256 = hash, length = bytes.Length } },
            };
            await File.WriteAllTextAsync(Path.Combine(root, "publish-manifest.json"), JsonSerializer.Serialize(publish));
            if (scenario == "changed") await File.WriteAllTextAsync(Path.Combine(root, "mp.exe"), "different-content");
            string tests = Path.Combine(root, "tests.json");
            await File.WriteAllTextAsync(tests,
                """{"schemaVersion":1,"suite":"managed","selected":3,"executed":3,"skipped":0,"categories":[{"category":"managed","selected":3,"executed":3,"skipped":0}]}""");
            string evidence = Path.Combine(root, "evidence.json");
            string rejected = Path.Combine(root, "rejected.json");
            await File.WriteAllTextAsync(rejected,
                """{"schemaVersion":1,"runtime":"win-x64","osVersion":"10.0.26100.0","osProductType":3,"cliRejected":true,"hostRejected":true,"stateUntouched":true}""");
            string installed = Path.Combine(root, "installed.json");
            if (scenario.StartsWith("installed-", StringComparison.Ordinal))
            {
                await File.WriteAllTextAsync(installed, JsonSerializer.Serialize(new
                {
                    schemaVersion = 1,
                    runtime = "win-x64",
                    packageSha256 = hash,
                    installedIdentity = true,
                    cliAlias = true,
                    hostAutoStart = true,
                    uninstalled = true,
                    cloudFilesExtension = true,
                    adapterAssociation = true,
                    minimumVersion = "10.0.26100.0",
                    osVersion = scenario == "installed-old-build" ? "10.0.22631.0" : "10.0.26100.0",
                    osProductType = scenario == "installed-server" ? 3 : 1,
                }));
            }
            var start = new ProcessStartInfo("pwsh")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[] { "-NoProfile", "-File", Path.Combine(repository, "eng", "collect-ci-evidence.ps1"),
                "-Job", "build-and-test", "-Runtime", "win-x64", "-OutputPath", evidence, "-TestManifests", tests, "-PublishDirectory", root,
                "-RejectedPath", rejected })
                start.ArgumentList.Add(argument);
            if (File.Exists(installed))
            {
                start.ArgumentList.Add("-InstalledPath");
                start.ArgumentList.Add(installed);
            }
            using Process process = Process.Start(start)!;
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.AreEqual(succeeds, process.ExitCode == 0, await output + await error);
            if (succeeds)
            {
                string report = await File.ReadAllTextAsync(evidence);
                Assert.DoesNotContain(root, report);
                using JsonDocument json = JsonDocument.Parse(report);
                Assert.AreEqual(2, json.RootElement.GetProperty("artifacts").GetArrayLength());
                Assert.AreEqual(40, json.RootElement.GetProperty("sourceSha").GetString()!.Length);
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}

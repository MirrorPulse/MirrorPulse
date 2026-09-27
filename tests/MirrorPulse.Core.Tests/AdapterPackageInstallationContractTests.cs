using System.IO.Compression;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterPackageInstallationContractTests
{
    private static readonly string[] ExpectedEntries = [
        "locales/en-US.json",
        "manifest.json",
        "worker/win-x64/MirrorPulse.Adapter.Worker.exe",
    ];

    [TestMethod]
    public async Task BuiltPackageCanBeInstalledAsAUserPackage()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-package-install-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "package");
        var output = Path.Combine(root, "release", "sample.mpadapter");
        var inbox = Path.Combine(root, "inbox");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "worker", "win-x64"));
            Directory.CreateDirectory(Path.Combine(source, "locales"));
            await File.WriteAllTextAsync(Path.Combine(source, "manifest.json"), "{\"schemaVersion\":1,\"adapterId\":\"com.example.adapter\"}");
            await File.WriteAllTextAsync(Path.Combine(source, "worker", "win-x64", "MirrorPulse.Adapter.Worker.exe"), "worker-binary");
            await File.WriteAllTextAsync(Path.Combine(source, "locales", "en-US.json"), "{\"label\":\"Example\"}");

            await AdapterPackageBuilder.BuildAsync(source, output);
            using (var archive = ZipFile.OpenRead(output))
            {
                CollectionAssert.AreEquivalent(
                    ExpectedEntries,
                    archive.Entries.Select(entry => entry.FullName).ToArray());
            }

            var receipt = await new CurrentUserAdapterInstallCommand(inbox).ExecuteAsync(output, developerMode: false);

            Assert.IsTrue(receipt.RequiresSignatureVerification);
            CollectionAssert.AreEqual(await File.ReadAllBytesAsync(output), await File.ReadAllBytesAsync(receipt.PackagePath));
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

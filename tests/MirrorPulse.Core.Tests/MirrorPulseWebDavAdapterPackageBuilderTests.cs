using System.IO.Compression;
using System.Text.Json;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavAdapterPackageBuilderTests
{
    [TestMethod]
    public async Task BuilderCreatesWebDavPackageWithNetworkCapabilities()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-webdav-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var x64 = Path.Combine(root, "webdav-x64.exe");
        var arm64 = Path.Combine(root, "webdav-arm64.exe");
        var output = Path.Combine(root, "webdav.mpadapter");
        await File.WriteAllTextAsync(x64, "x64");
        await File.WriteAllTextAsync(arm64, "arm64");
        try
        {
            var result = await MirrorPulseWebDavAdapterPackageBuilder.BuildAsync(
                new MirrorPulseWebDavAdapterPackageInput("1.0.0", x64, arm64),
                output);

            using var archive = ZipFile.OpenRead(result.PackagePath);
            var manifest = archive.GetEntry("manifest.json");
            Assert.IsNotNull(manifest);
            using var document = JsonDocument.Parse(manifest!.Open());
            Assert.AreEqual("com.mirrorpulse.adapter.webdav", document.RootElement.GetProperty("adapterId").GetString());
            Assert.IsTrue(document.RootElement.GetProperty("capabilities").GetProperty("network").GetBoolean());
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "worker/win-arm64/webdav-arm64.exe");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

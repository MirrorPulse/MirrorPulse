using System.IO.Compression;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryAdapterPackageBuilderTests
{
    [TestMethod]
    public async Task BuilderCreatesAWindowsDualArchitectureMpadapter()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var output = Path.Combine(root, "local-directory.mpadapter");
        var x64 = Path.Combine(root, "local-x64.exe");
        var arm64 = Path.Combine(root, "local-arm64.exe");
        await File.WriteAllTextAsync(x64, "x64");
        await File.WriteAllTextAsync(arm64, "arm64");
        try
        {
            var result = await MirrorPulseLocalDirectoryAdapterPackageBuilder.BuildAsync(
                new MirrorPulseLocalDirectoryAdapterPackageInput("1.0.0", x64, arm64),
                output);

            Assert.AreEqual(output, result.PackagePath);
            Assert.IsTrue(File.Exists(output));
            using var archive = ZipFile.OpenRead(output);
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "manifest.json");
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "worker/win-x64/local-x64.exe");
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "worker/win-arm64/local-arm64.exe");
            CollectionAssert.Contains(archive.Entries.Select(entry => entry.FullName).ToArray(), "locales/en-US.json");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task BuilderRejectsNonExecutablePayloads()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var x64 = Path.Combine(root, "local-x64.dll");
        var arm64 = Path.Combine(root, "local-arm64.exe");
        await File.WriteAllTextAsync(x64, "x64");
        await File.WriteAllTextAsync(arm64, "arm64");
        try
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => MirrorPulseLocalDirectoryAdapterPackageBuilder.BuildAsync(
                new MirrorPulseLocalDirectoryAdapterPackageInput("1.0.0", x64, arm64),
                Path.Combine(root, "local.mpadapter")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

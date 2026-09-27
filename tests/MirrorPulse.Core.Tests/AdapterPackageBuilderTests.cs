using System.IO.Compression;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterPackageBuilderTests
{
    private static readonly string[] ExpectedEntries = ["manifest.json", "worker/worker.exe"];

    [TestMethod]
    public async Task BuilderCreatesMpadapterWithSortedRelativeEntries()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-package-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source");
        var output = Path.Combine(root, "release", "sample.mpadapter");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "worker"));
            await File.WriteAllTextAsync(Path.Combine(source, "manifest.json"), "{}");
            await File.WriteAllTextAsync(Path.Combine(source, "worker", "worker.exe"), "worker");

            var result = await AdapterPackageBuilder.BuildAsync(source, output);
            using var archive = ZipFile.OpenRead(output);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();

            Assert.AreEqual(output, result.PackagePath);
            Assert.HasCount(2, result.FileManifest.Files);
            CollectionAssert.AreEqual(ExpectedEntries, names);
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
    public async Task BuilderRejectsOutputInsideSourceAndWrongExtension()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-package-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => AdapterPackageBuilder.BuildAsync(root, Path.Combine(root, "inside.mpadapter")));
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => AdapterPackageBuilder.BuildAsync(root, Path.Combine(Path.GetTempPath(), "package.zip")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

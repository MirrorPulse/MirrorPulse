using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryScannerTests
{
    private static readonly string[] ExpectedPaths = ["docs", "docs/a.txt", "root.txt"];

    [TestMethod]
    public async Task ScannerReturnsDeterministicFilesAndDirectories()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-scan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(source, "docs"));
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "docs", "a.txt"), "abc");
            await File.WriteAllTextAsync(Path.Combine(source, "root.txt"), "x");

            var entries = MirrorPulseLocalDirectoryScanner.Scan(source);

            Assert.HasCount(3, entries);
            CollectionAssert.AreEqual(ExpectedPaths, entries.Select(entry => entry.RelativePath).ToArray());
            Assert.AreEqual(MirrorPulseLocalDirectoryEntryKind.Directory, entries[0].Kind);
            Assert.AreEqual(3, entries[1].Length);
            Assert.AreEqual(1, entries[2].Length);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public void ScannerRejectsMissingRoots()
    {
        Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
            MirrorPulseLocalDirectoryScanner.Scan(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }
}

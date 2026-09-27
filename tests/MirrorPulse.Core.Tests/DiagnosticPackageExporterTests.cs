using System.IO.Compression;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Diagnostics;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class DiagnosticPackageExporterTests
{
    private static readonly string[] ExpectedEntries = ["diagnostics.jsonl", "logs/mirrorpulse.log"];

    [TestMethod]
    public async Task ExporterCreatesLocalZipWithRedactedDiagnosticProperties()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-diagnostics-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var log = Path.Combine(root, "mirrorpulse.log");
            await File.WriteAllTextAsync(log, "local log");
            var eventData = new DiagnosticEvent(
                Guid.NewGuid(),
                "host",
                new Diagnostic("sync.failed", "Sync failed", DiagnosticSeverity.Error),
                DateTimeOffset.UtcNow,
                properties: new Dictionary<string, string> { ["access_token"] = "private-value" });
            var output = await DiagnosticPackageExporter.ExportAsync(Path.Combine(root, "diagnostics.zip"), [eventData], [log]);

            using var archive = ZipFile.OpenRead(output);
            CollectionAssert.AreEquivalent(ExpectedEntries, archive.Entries.Select(entry => entry.FullName).ToArray());
            var diagnostics = await new StreamReader(archive.GetEntry("diagnostics.jsonl")!.Open()).ReadToEndAsync();
            StringAssert.Contains(diagnostics, LogFieldPolicy.RedactedValue);
            Assert.IsFalse(diagnostics.Contains("private-value", StringComparison.Ordinal));
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

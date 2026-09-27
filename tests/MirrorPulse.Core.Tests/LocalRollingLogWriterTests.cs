using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Diagnostics;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LocalRollingLogWriterTests
{
    [TestMethod]
    public async Task WriterRotatesLocalFilesAndRedactsStructuredSecrets()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-logs-{Guid.NewGuid():N}");
        try
        {
            using var writer = new LocalRollingLogWriter(root, maxFileBytes: 240, backupCount: 2);
            for (var index = 0; index < 6; index++)
            {
                await writer.WriteAsync(new LogEntry(LogLevel.Information, "sync", "Transfer complete", DateTimeOffset.UtcNow,
                    [new LogField("access_token", "private-value")]));
            }

            Assert.IsTrue(File.Exists(writer.FilePath));
            Assert.IsTrue(File.Exists(writer.FilePath + ".1"));
            Assert.IsFalse(File.Exists(writer.FilePath + ".3"));
            var contents = string.Join(' ', Directory.GetFiles(root).Select(File.ReadAllText));
            StringAssert.Contains(contents, LogFieldPolicy.RedactedValue);
            Assert.IsFalse(contents.Contains("private-value", StringComparison.Ordinal));
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

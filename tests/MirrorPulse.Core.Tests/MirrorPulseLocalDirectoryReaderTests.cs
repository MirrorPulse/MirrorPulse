using System.Text;
using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryReaderTests
{
    [TestMethod]
    public async Task ReaderReturnsCompleteAndBoundedFileRanges()
    {
        var source = CreateDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(source, "file.txt"), "0123456789");
            var reader = new MirrorPulseLocalDirectoryReader(source);

            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("0123456789"), await reader.ReadAsync("file.txt"));
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("2345"), await reader.ReadRangeAsync("file.txt", 2, 4));
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("89"), await reader.ReadRangeAsync("file.txt", 8, 20));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public void ReaderRejectsPathsOutsideTheSourceDirectory()
    {
        var source = CreateDirectory();
        try
        {
            var reader = new MirrorPulseLocalDirectoryReader(source);
            Assert.ThrowsExactly<ArgumentException>(() => reader.ReadAsync("../secret.txt"));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-reader-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

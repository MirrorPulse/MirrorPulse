using System.Text;
using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryWriterTests
{
    [TestMethod]
    public async Task WriterCreatesParentDirectoriesAndAtomicallyReplacesFiles()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        try
        {
            var writer = new MirrorPulseLocalDirectoryWriter(source);
            var first = await writer.WriteAsync("nested/file.txt", Encoding.UTF8.GetBytes("first"));
            var second = await writer.WriteAsync("nested/file.txt", Encoding.UTF8.GetBytes("second"));

            Assert.AreEqual("nested/file.txt", first.RelativePath);
            Assert.AreEqual(5, first.Length);
            Assert.AreEqual(6, second.Length);
            Assert.AreEqual("second", await File.ReadAllTextAsync(Path.Combine(source, "nested", "file.txt")));
            Assert.IsEmpty(Directory.EnumerateFiles(source, "*.mp-tmp", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriterRejectsTraversalBeforeCreatingFiles()
    {
        var source = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        try
        {
            var writer = new MirrorPulseLocalDirectoryWriter(source);
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => writer.WriteAsync("../outside.txt", ReadOnlyMemory<byte>.Empty));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }
}

using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterDownloadIntegrityRecorderTests
{
    [TestMethod]
    public async Task RecorderCapturesDownloadedLengthAndSha256()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-integrity-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "package.mpadapter");
            var bytes = "package-content"u8.ToArray();
            await File.WriteAllBytesAsync(path, bytes);
            var package = new DownloadedAdapterPackage(
                new Uri("https://github.com/MirrorPulse/example/releases/download/v1.0.0/example.mpadapter"),
                path,
                bytes.LongLength);

            var integrity = await AdapterDownloadIntegrityRecorder.ComputeAsync(package);

            Assert.AreEqual(bytes.LongLength, integrity.Length);
            Assert.AreEqual(Sha256Digest.Compute(bytes), integrity.Sha256);
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

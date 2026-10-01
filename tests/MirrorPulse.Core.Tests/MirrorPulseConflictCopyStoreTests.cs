using System.Text;
using System.Text.Json;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseConflictCopyStoreTests
{
    [TestMethod]
    public async Task CommittedCopyIsVerifiedAndPartialCopyNeverAuthorizesDestruction()
    {
        string fixture = Path.Combine(Path.GetTempPath(), "MirrorPulse-copy-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(fixture, "sync"), Path.Combine(fixture, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);
        string source = Path.Combine(paths.SyncRootPath, "note.txt");
        await File.WriteAllTextAsync(source, "original content");
        var store = new MirrorPulseConflictCopyStore(paths);
        MirrorPulseConflictRecord conflict = Create();
        try
        {
            string path = await store.PreserveAsync(conflict, MirrorPulseConflictPreservedSide.Local, token => store.OpenLocalAsync(conflict, token));
            MirrorPulseConflictCopyManifest manifest = JsonSerializer.Deserialize<MirrorPulseConflictCopyManifest>(await File.ReadAllTextAsync(path + ".manifest.json"))!;
            Assert.AreEqual("complete", manifest.State);
            Assert.AreEqual(new FileInfo(path).Length, manifest.Length);
            Assert.AreEqual(64, manifest.Sha256!.Length);
            Assert.IsFalse(File.Exists(path + ".pending.json"));
            Assert.AreEqual("original content", await File.ReadAllTextAsync(source));
            MirrorPulseConflictRecord failing = Create();
            await Assert.ThrowsExactlyAsync<IOException>(() => store.PreserveAsync(failing, MirrorPulseConflictPreservedSide.Remote,
                _ => ValueTask.FromResult<Stream>(new FailingSource())));
            string directory = Path.Combine(MirrorPulseConflictDirectory.GetPath(paths, failing.InstanceId), "remote");
            string failedPath = Path.Combine(directory, MirrorPulseConflictFileName.Create(failing));
            Assert.IsFalse(File.Exists(failedPath));
            Assert.IsFalse(File.Exists(failedPath + ".manifest.json"));
            Assert.IsTrue(File.Exists(failedPath + ".pending.json"));
            Assert.AreEqual("original content", await File.ReadAllTextAsync(source));
        }
        finally { Directory.Delete(fixture, true); }
    }

    private sealed class FailingSource : MemoryStream
    {
        private bool _read;
        public FailingSource() : base(Encoding.UTF8.GetBytes("partial content")) { }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_read) throw new IOException("fixture source failure after partial bytes");
            _read = true;
            return base.ReadAsync(buffer[..Math.Min(3, buffer.Length)], cancellationToken);
        }
    }

    internal static MirrorPulseConflictRecord Create() => new(Guid.NewGuid(), InstanceId.New(), "change", "note.txt",
        MirrorPulseConflictReason.Content, MirrorPulseVersionComparison.Diverged, "base", "remote", DateTimeOffset.UtcNow);
}

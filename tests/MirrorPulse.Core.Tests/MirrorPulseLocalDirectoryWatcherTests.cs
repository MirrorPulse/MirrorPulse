using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryWatcherTests
{
    [TestMethod]
    public async Task WorkerWritesAreSuppressedWhileExternalSourceChangesRemainVisible()
    {
        string source = CreateDirectory();
        try
        {
            var echo = new MirrorPulseLocalDirectoryEchoSuppressor();
            using var watcher = new MirrorPulseLocalDirectoryWatcher(source, echo);
            var received = new System.Collections.Concurrent.ConcurrentQueue<MirrorPulseLocalDirectoryChangeEventArgs>();
            var external = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += (_, change) =>
            {
                received.Enqueue(change);
                if (change.RelativePath == "nested/external.txt")
                {
                    external.TrySetResult();
                }
            };
            watcher.Start();

            var writer = new MirrorPulseLocalDirectoryWriter(source, echo);
            await writer.WriteAsync("nested/synced.txt", "worker write"u8.ToArray());
            await File.WriteAllTextAsync(Path.Combine(source, "nested", "external.txt"), "user write");
            Assert.AreSame(external.Task, await Task.WhenAny(external.Task, Task.Delay(TimeSpan.FromSeconds(5))));
            await Task.Delay(100);
            Assert.IsFalse(received.Any(change => change.RelativePath.Contains("synced.txt", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public async Task WatcherReportsCreatedFilesAsNormalizedRelativeChanges()
    {
        var source = CreateDirectory();
        try
        {
            using var watcher = new MirrorPulseLocalDirectoryWatcher(source);
            var received = new TaskCompletionSource<MirrorPulseLocalDirectoryChangeEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            watcher.Changed += (_, change) =>
            {
                if (change.Kind == MirrorPulseLocalDirectoryChangeKind.Created)
                {
                    received.TrySetResult(change);
                }
            };

            watcher.Start();
            await File.WriteAllTextAsync(Path.Combine(source, "nested", "file.txt"), "content");
            var completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(received.Task, completed);
            var change = await received.Task;
            Assert.AreEqual("nested/file.txt", change.RelativePath);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public void ChangeNormalizesSeparatorsAndRequiresOldPathForRenames()
    {
        var change = new MirrorPulseLocalDirectoryChangeEventArgs(
            MirrorPulseLocalDirectoryChangeKind.Renamed,
            "nested\\new.txt",
            "nested\\old.txt");

        Assert.AreEqual("nested/new.txt", change.RelativePath);
        Assert.AreEqual("nested/old.txt", change.OldRelativePath);
        Assert.ThrowsExactly<ArgumentNullException>(() => new MirrorPulseLocalDirectoryChangeEventArgs(
            MirrorPulseLocalDirectoryChangeKind.Renamed,
            "new.txt"));
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-watcher-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(path, "nested"));
        return path;
    }
}

using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseActiveRemotePollerTests
{
    [TestMethod]
    public async Task PollerTurnsRemoteSnapshotChangesIntoOrderedBatches()
    {
        InstanceId instance = InstanceId.New();
        RootRegistration root = AdapterRootRegistrationMapper.Map(
            AdapterId.Parse("example.drive"), instance,
            new AdapterRootDefinition("files", "Documents", "Documents", false),
            RootRegistrationState.Active);
        var source = new FakeDirectorySource();
        var batches = new List<CloudRemoteChangeBatch>();
        var adapter = new AdapterInstance(
            root.AdapterId, InstallId.New(), instance, "Remote files", new Dictionary<string, string>(), [],
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "files"),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "transfers"),
            true, AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
        await using var poller = new MirrorPulseActiveRemotePoller(
            source, [adapter], [root],
            (id, batch, _) =>
            {
                Assert.AreEqual(instance, id);
                batches.Add(batch);
                return ValueTask.CompletedTask;
            });

        source.Set(new FakeEntry("file-1", "v1", CloudItemKind.File, "report.bin", 3));
        Assert.IsFalse(await poller.PollOnceAsync(instance));

        source.Set(new FakeEntry("file-1", "v2", CloudItemKind.File, "renamed.bin", 4));
        Assert.IsTrue(await poller.PollOnceAsync(instance));
        Assert.HasCount(1, batches);
        Assert.AreEqual(CloudRemoteChangeKind.Move, batches[0].Changes.Single().Kind);
        Assert.AreEqual("Documents\\renamed.bin", batches[0].Changes.Single().RelativePath);
        Assert.AreEqual("Documents\\report.bin", batches[0].Changes.Single().PreviousRelativePath);
        Assert.AreEqual("v1", batches[0].Changes.Single().PreviousRemoteRevision);

        source.Set();
        Assert.IsTrue(await poller.PollOnceAsync(instance));
        Assert.AreEqual(CloudRemoteChangeKind.Delete, batches[1].Changes.Single().Kind);
        Assert.AreEqual("v2", batches[1].Changes.Single().RemoteRevision);
        CollectionAssert.AreEqual(batches[0].FinalCursor.ToArray(), batches[0].Changes[0].CursorAfter.ToArray());
    }

    [TestMethod]
    public async Task PollerRestoresSnapshotAcrossProcessInstances()
    {
        string dataRoot = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataRoot);
        try
        {
            InstanceId instance = InstanceId.New();
            RootRegistration root = AdapterRootRegistrationMapper.Map(
                AdapterId.Parse("example.drive"), instance,
                new AdapterRootDefinition("files", "Documents", "Documents", false),
                RootRegistrationState.Active);
            var adapter = new AdapterInstance(
                root.AdapterId, InstallId.New(), instance, "Remote files", new Dictionary<string, string>(), [],
                Path.Combine(dataRoot, "files"), Path.Combine(dataRoot, "transfers"), true,
                AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
            var source = new FakeDirectorySource();
            var store = new MirrorPulseFileRemotePollSnapshotStore(dataRoot);
            source.Set(new FakeEntry("file-1", "v1", CloudItemKind.File, "report.bin", 3));
            await using (var first = new MirrorPulseActiveRemotePoller(
                source, [adapter], [root], (_, _, _) => ValueTask.CompletedTask,
                snapshotStore: store))
            {
                Assert.IsFalse(await first.PollOnceAsync(instance));
            }

            var batches = new List<CloudRemoteChangeBatch>();
            await using (var second = new MirrorPulseActiveRemotePoller(
                source, [adapter], [root], (_, batch, _) =>
                {
                    batches.Add(batch);
                    return ValueTask.CompletedTask;
                }, snapshotStore: store))
            {
                Assert.IsFalse(await second.PollOnceAsync(instance));
            }

            source.Set(new FakeEntry("file-1", "v2", CloudItemKind.File, "renamed.bin", 4));
            await using (var third = new MirrorPulseActiveRemotePoller(
                source, [adapter], [root], (_, batch, _) =>
                {
                    batches.Add(batch);
                    return ValueTask.CompletedTask;
                }, snapshotStore: store))
            {
                Assert.IsTrue(await third.PollOnceAsync(instance));
            }

            Assert.AreEqual(CloudRemoteChangeKind.Move, batches.Single().Changes.Single().Kind);
            Assert.AreEqual("Documents\\report.bin", batches.Single().Changes.Single().PreviousRelativePath);
        }
        finally
        {
            if (Directory.Exists(dataRoot)) Directory.Delete(dataRoot, recursive: true);
        }
    }

    [TestMethod]
    public async Task PollerLeavesMultiRootInstancesForExplicitRemoteRootMapping()
    {
        InstanceId instance = InstanceId.New();
        AdapterId adapterId = AdapterId.Parse("example.multi-root");
        IReadOnlyList<RootRegistration> roots = AdapterRootRegistrationMapper.MapAll(
            adapterId, instance,
            [new AdapterRootDefinition("documents", "Documents", "Documents", false),
             new AdapterRootDefinition("photos", "Photos", "Photos", false)],
            RootRegistrationState.Active);
        var adapter = new AdapterInstance(
            adapterId, InstallId.New(), instance, "Multi-root", new Dictionary<string, string>(), [],
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "files"),
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "transfers"),
            true, AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
        await using var poller = new MirrorPulseActiveRemotePoller(
            new FakeDirectorySource(), [adapter], roots, (_, _, _) => ValueTask.CompletedTask);

        Assert.IsFalse(await poller.PollOnceAsync(instance));
    }

    private sealed class FakeDirectorySource : IMirrorPulseDirectoryPageSource
    {
        private readonly Dictionary<string, CloudRemoteDirectoryEntry> _entries = new(StringComparer.Ordinal);

        public void Set(params FakeEntry[] entries)
        {
            _entries.Clear();
            foreach (FakeEntry entry in entries)
            {
                CloudPlaceholderMetadata metadata = entry.Kind == CloudItemKind.Directory
                    ? CloudPlaceholderMetadata.CreateDirectoryBuilder().Build()
                    : CloudPlaceholderMetadata.CreateFileBuilder().Build();
                if (entry.Kind == CloudItemKind.File)
                {
                    metadata = CloudPlaceholderMetadata.CreateFileBuilder()
                        .WithLastWriteTime(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
                        .Build();
                }
                _entries.Add(entry.RemoteId, new CloudRemoteDirectoryEntry(entry.RemoteId,
                    entry.Revision, entry.Kind, entry.Path, null, entry.Length, metadata, false));
            }
        }

        public ValueTask<CloudRemoteDirectoryPage> ReadPageAsync(
            InstanceId instanceId,
            string normalizedPath,
            ReadOnlyMemory<byte> continuationCursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            CloudRemoteDirectoryEntry[] entries = _entries.Values
                .Where(entry => !entry.RelativePath.Contains('/'))
                .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .ToArray();
            return ValueTask.FromResult(new CloudRemoteDirectoryPage(entries,
                ReadOnlyMemory<byte>.Empty, true));
        }
    }

    private sealed record FakeEntry(
        string RemoteId,
        string Revision,
        CloudItemKind Kind,
        string Path,
        long Length);
}

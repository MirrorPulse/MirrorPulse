using System.Runtime.Versioning;
using System.Security.Cryptography;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
[SupportedOSPlatform("windows10.0.19041")]
public sealed class MirrorPulseFullRescanPolicyTests
{
    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    public async Task NativeOverflowReconcilesFilesAndKeepsDisabledRootsOffline()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
            Assert.Inconclusive("Requires the disposable NativeCloudFiles verification environment.");
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var registry = new CfSharpMirrorPulseCloudRootRegistry();
        InstanceId active = InstanceId.New();
        InstanceId offline = InstanceId.New();
        RootRegistration Registration(InstanceId instance, string name, RootRegistrationState state) =>
            AdapterRootRegistrationMapper.Map(AdapterId.Parse("example.rescan"), instance,
                new AdapterRootDefinition(name, name, name, false), state);
        RootRegistration first = Registration(active, "Docs", RootRegistrationState.Active);
        RootRegistration second = Registration(offline, "Offline", RootRegistrationState.Disabled);
        var transport = new DiskWorker(Path.Combine(root, "remote"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            Directory.CreateDirectory(Path.Combine(paths.SyncRootPath, "Docs"));
            Directory.CreateDirectory(Path.Combine(paths.SyncRootPath, "Offline"));
            await File.WriteAllTextAsync(Path.Combine(paths.SyncRootPath, "Docs", "note.txt"), "active local data", timeout.Token);
            await File.WriteAllTextAsync(Path.Combine(paths.SyncRootPath, "Offline", "offline.txt"), "offline local data", timeout.Token);
            registry.Register(new(paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]));
            var state = new MirrorPulseCfSharpStateSession(paths);
            await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths).WithStateStore(state)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath)).Build();
            await fileSystem.StartAsync(timeout.Token);
            await using CloudLocalChangeFeed feed = fileSystem.CreateLocalChangeFeed(new() { BufferCapacity = 1 });
            await feed.StartAsync(timeout.Token);
            // Backpressure the official store while real native notifications fill the public feed buffer.
            await using (ICloudStateTransaction transaction = await state.OpenStore.BeginTransactionAsync(timeout.Token))
            {
                for (int index = 0; index < 128; index++)
                {
                    string path = Path.Combine(paths.SyncRootPath, "Docs", $"churn-{index}.txt");
                    File.WriteAllText(path, "overflow");
                    File.Delete(path);
                }
                await Task.Delay(500, timeout.Token);
                await transaction.RollbackAsync(timeout.Token);
            }
            CloudLocalChangeBatch signal;
            do
            {
                signal = await feed.ReadBatchAsync(timeout.Token);
                if (!signal.RequiresFullRescan) await feed.AcknowledgeAsync(signal.Changes.Select(change => change.OperationId), timeout.Token);
            } while (!signal.RequiresFullRescan);
            await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths, timeout.Token);
            var router = new MirrorPulseRootRouter(paths.SyncRootPath, [first, second]);
            var policy = new MirrorPulseFullRescanPolicy(fileSystem, feed, state, router, catalog, transport, transport,
                instance => instance == active, transport, transport, transport);
            await Assert.ThrowsExactlyAsync<IOException>(() => new MirrorPulseCfSharpFullRescanAdapter(feed, policy.ReconcileAsync)
                .HandleAsync(signal, timeout.Token).AsTask());
            Assert.AreEqual(1, transport.Uploads.GetValueOrDefault(active));
            Assert.AreEqual(0, transport.Uploads.GetValueOrDefault(offline));
            Assert.AreEqual("active local data", await File.ReadAllTextAsync(transport.PathFor(active, "note.txt"), timeout.Token));
            Assert.AreEqual(CloudSynchronizationState.InSync,
                (await fileSystem.GetFile("Docs/note.txt").InspectAsync(timeout.Token)).SynchronizationState);
            RootRegistration enabledSecond = Registration(offline, "Offline", RootRegistrationState.Active);
            policy = new(fileSystem, feed, state, new(paths.SyncRootPath, [first, enabledSecond]), catalog, transport, transport,
                _ => true, transport, transport, transport);
            MirrorPulseFullRescanResult result = await new MirrorPulseCfSharpFullRescanAdapter(feed, policy.ReconcileAsync).HandleAsync(signal, timeout.Token);
            Assert.IsTrue(result.WasRequired);
            Assert.AreEqual(1, transport.Uploads[active]);
            Assert.AreEqual(1, transport.Uploads[offline]);
            Assert.AreEqual("offline local data", await File.ReadAllTextAsync(transport.PathFor(offline, "offline.txt"), timeout.Token));
            await File.WriteAllTextAsync(Path.Combine(paths.SyncRootPath, "Docs", "after.txt"), "after rescan", timeout.Token);
            Assert.IsFalse((await feed.ReadBatchAsync(timeout.Token)).RequiresFullRescan);
        }
        finally
        {
            registry.Unregister(paths.SyncRootPath);
            Directory.Delete(root, true);
        }
    }

    private sealed class DiskWorker(string root) : IMirrorPulseWorkerUploadTransport, IMirrorPulseWorkerStatTransport,
        IMirrorPulseWorkerRangeTransport, IMirrorPulseWorkerDirectoryPageSource, IMirrorPulseWorkerMutationTransport
    {
        public Dictionary<InstanceId, int> Uploads { get; } = [];
        public string PathFor(InstanceId instance, string path) => Path.Combine(root, instance.ToString(), path.Replace('/', Path.DirectorySeparatorChar));
        private string? Revision(InstanceId instance, string path) => File.Exists(PathFor(instance, path))
            ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(PathFor(instance, path)))) : null;
        public ValueTask<string?> StatAsync(MirrorPulseWorkerStatRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Revision(request.InstanceId, request.NormalizedPath));
        public async ValueTask<string> UploadAsync(MirrorPulseWorkerUploadRequest request, CancellationToken cancellationToken)
        {
            string? actual = Revision(request.InstanceId, request.NormalizedPath);
            if (actual != request.ExpectedRevision) throw new MirrorPulseWorkerMutationConflictException(request.ExpectedRevision, actual);
            string path = PathFor(request.InstanceId, request.NormalizedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await using (var target = File.Create(path)) await request.Content.CopyToAsync(target, cancellationToken);
            Uploads[request.InstanceId] = Uploads.GetValueOrDefault(request.InstanceId) + 1;
            return Revision(request.InstanceId, request.NormalizedPath)!;
        }
        public ValueTask<MirrorPulseWorkerDirectoryPage> ReadDirectoryPageAsync(MirrorPulseWorkerDirectoryPageRequest request, CancellationToken cancellationToken)
        {
            string path = PathFor(request.InstanceId, request.NormalizedPath);
            MirrorPulseWorkerDirectoryEntry[] entries = Directory.Exists(path) ? Directory.GetFiles(path).Select(file =>
            {
                string relative = Path.GetRelativePath(PathFor(request.InstanceId, string.Empty), file).Replace('\\', '/');
                return new MirrorPulseWorkerDirectoryEntry("remote:" + relative, Revision(request.InstanceId, relative)!, "file", relative,
                    new FileInfo(file).Length, null, null, false);
            }).ToArray() : [];
            return ValueTask.FromResult(new MirrorPulseWorkerDirectoryPage(entries, ReadOnlyMemory<byte>.Empty, true));
        }
        public ValueTask<Stream> ReadRangeAsync(MirrorPulseWorkerReadRangeRequest request, CancellationToken cancellationToken) =>
            ValueTask.FromResult<Stream>(new MemoryStream(File.ReadAllBytes(PathFor(request.InstanceId, request.NormalizedPath))
                .AsSpan((int)request.Offset, (int)request.Length).ToArray()));
        public ValueTask<string?> DeleteAsync(MirrorPulseWorkerDeleteRequest request, CancellationToken cancellationToken)
        {
            File.Delete(PathFor(request.InstanceId, request.NormalizedPath));
            return ValueTask.FromResult<string?>(null);
        }
        public ValueTask<string> MoveAsync(MirrorPulseWorkerMoveRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

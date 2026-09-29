using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseRemoteBatchCoordinatorTests
{
    [TestMethod]
    [DoNotParallelize]
    public async Task NativeBatchCursorSurvivesCrashGapAndRejectsFingerprintReuse()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var definition = new MirrorPulseSyncRootDefinition(paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]);
        var cloud = new CfSharpMirrorPulseCloudRootRegistry();
        var instance = InstanceId.New();
        try
        {
            cloud.Register(definition);
            var first = Batch(instance, "first", ReadOnlyMemory<byte>.Empty, new byte[] { 1 });
            var second = Batch(instance, "second", new byte[] { 1 }, new byte[] { 2 });
            var crashGap = Batch(instance, "gap", new byte[] { 2 }, new byte[] { 3 });
            var firstState = new MirrorPulseCfSharpStateSession(paths);
            await using (var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths)
                .WithStateStore(firstState)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                .Build())
            {
                await fileSystem.StartAsync();
                var coordinator = new MirrorPulseRemoteBatchCoordinator(fileSystem, firstState);
                CloudRemoteApplyResult firstResult = await coordinator.ApplyAsync(instance, first);
                Assert.AreEqual(CloudRemoteBatchStatus.Applied, firstResult.Status);
                CollectionAssert.AreEqual(new byte[] { 1 }, (await coordinator.ReadCursorAsync(instance))?.ToArray());
                await coordinator.ApplyAsync(instance, first);
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() => coordinator.ApplyAsync(
                    instance, Batch(instance, "first", ReadOnlyMemory<byte>.Empty, new byte[] { 9 })).AsTask());

                await coordinator.ApplyAsync(instance, second);
                CloudRemoteApplyResult gapResult = await fileSystem.ApplyRemoteChangesAsync(crashGap);
                Assert.AreEqual(CloudRemoteBatchStatus.Applied, gapResult.Status);
                CollectionAssert.AreEqual(new byte[] { 2 }, (await coordinator.ReadCursorAsync(instance))?.ToArray());
            }

            var secondState = new MirrorPulseCfSharpStateSession(paths);
            await using (var reopened = new MirrorPulseCloudFileSystemBuilder(paths)
                .WithStateStore(secondState)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                .Build())
            {
                await reopened.StartAsync();
                var coordinator = new MirrorPulseRemoteBatchCoordinator(reopened, secondState);
                await coordinator.ApplyAsync(instance, crashGap);
                CollectionAssert.AreEqual(new byte[] { 3 }, (await coordinator.ReadCursorAsync(instance))?.ToArray());

                var metadata = CloudPlaceholderMetadata.CreateDirectoryBuilder().Build();
                var partial = new CloudRemoteChangeBatch(
                    $"{instance}/partial", new byte[] { 3 },
                    [new CloudRemoteChange("a", CloudRemoteChangeKind.DirectoryUpsert, "a", "v1",
                        CloudItemKind.Directory, "A", metadata: metadata, cursorAfter: new byte[] { 31 }),
                     new CloudRemoteChange("b", CloudRemoteChangeKind.DirectoryUpsert, "b", "v1",
                        CloudItemKind.Directory, "B", metadata: metadata, cursorAfter: new byte[] { 4 })],
                    new byte[] { 4 });
                CloudRemoteApplyResult partialResult = await coordinator.ApplyAsync(
                    instance, partial, new CloudRemoteApplyOptions { MaximumEntries = 1 });
                Assert.IsTrue(partialResult.RequiresRetry);
                Assert.IsLessThanOrEqualTo(1, partialResult.AppliedEntryCount);
                CollectionAssert.AreEqual(new byte[] { 3 }, (await coordinator.ReadCursorAsync(instance))?.ToArray());
            }
        }
        finally
        {
            cloud.Unregister(paths.SyncRootPath);
            Directory.Delete(root, recursive: true);
        }
    }

    private static CloudRemoteChangeBatch Batch(
        InstanceId instance,
        string id,
        ReadOnlyMemory<byte> before,
        ReadOnlyMemory<byte> after) => new($"{instance}/{id}", before, [], after);
}

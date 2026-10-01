using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseRemoteEchoIntegrationTests
{
    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    public async Task NativeRemoteMetadataApplicationDoesNotEnterLocalUploadJournal()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
        {
            Assert.Inconclusive("Requires the NativeCloudFiles test environment; run the dedicated verification gate.");
        }

        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var definition = new MirrorPulseSyncRootDefinition(paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]);
        var cloud = new CfSharpMirrorPulseCloudRootRegistry();
        var instance = InstanceId.New();
        RootRegistration registration = AdapterRootRegistrationMapper.Map(
            AdapterId.Parse("example.local"), instance,
            new AdapterRootDefinition("docs", "Documents", "Documents", false),
            RootRegistrationState.Active);
        try
        {
            cloud.Register(definition);
            var state = new MirrorPulseCfSharpStateSession(paths);
            await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths)
                .WithStateStore(state)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                .Build();
            await fileSystem.StartAsync();
            await using CloudLocalChangeFeed feed = fileSystem.CreateLocalChangeFeed();
            await feed.StartAsync();
            var router = new MirrorPulseRootRouter(paths.SyncRootPath, [registration]);
            await new MirrorPulseRootPopulationCoordinator(fileSystem, feed).PopulateAsync(router);

            CloudItemState item;
            await using (ICloudStateTransaction transaction = await state.OpenStore.BeginTransactionAsync())
            {
                item = await transaction.Items.GetByRelativePathAsync("Documents")
                    ?? throw new InvalidDataException("The root placeholder has no CfSharp item state.");
                await transaction.RollbackAsync();
            }

            var metadata = CloudPlaceholderMetadata.CreateDirectoryBuilder().Build();
            var change = new CloudRemoteChange(
                "metadata-1", CloudRemoteChangeKind.MetadataUpdate,
                item.RemoteId, "remote-r1", CloudItemKind.Directory, "Documents",
                item.ItemId, previousRemoteRevision: string.IsNullOrWhiteSpace(item.RemoteRevision)
                    ? null : item.RemoteRevision,
                metadata: metadata, cursorAfter: new byte[] { 1 });
            var batch = new CloudRemoteChangeBatch(
                $"{instance}/metadata", ReadOnlyMemory<byte>.Empty, [change], new byte[] { 1 });
            CloudRemoteApplyResult result = await fileSystem.ApplyRemoteChangesAsync(batch);
            Assert.AreEqual(CloudRemoteBatchStatus.Applied, result.Status,
                string.Join("; ", result.Entries.Select(entry => $"{entry.Status}: {entry.Error}")));
            await Task.Delay(300);
            await using ICloudStateTransaction journal = await state.OpenStore.BeginTransactionAsync();
            Assert.IsEmpty(await journal.Operations.ListAsync(16));
            await journal.RollbackAsync();
        }
        finally
        {
            cloud.Unregister(paths.SyncRootPath);
            Directory.Delete(root, recursive: true);
        }
    }
}

using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseJournalUploadSourceTests
{
    [TestMethod]
    public async Task NativeUnacknowledgedJournalSurvivesRestartAndDisabledInstance()
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
        RootRegistration registration = AdapterRootRegistrationMapper.Map(
            AdapterId.Parse("example.local"), instance,
            new AdapterRootDefinition("docs", "Documents", "Documents", false),
            RootRegistrationState.Active);
        var router = new MirrorPulseRootRouter(paths.SyncRootPath, [registration]);
        try
        {
            cloud.Register(definition);
            Guid originalId = Guid.Empty;
            for (int run = 0; run < 3; run++)
            {
                await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths)
                    .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                    .Build();
                await fileSystem.StartAsync();
                CloudLocalChangeFeed feed = fileSystem.CreateLocalChangeFeed();
                await feed.StartAsync();
                if (run == 0)
                {
                    await new MirrorPulseRootPopulationCoordinator(fileSystem, feed).PopulateAsync(router);
                }
                await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
                if (run == 0)
                {
                    await File.WriteAllTextAsync(
                        Path.Combine(paths.SyncRootPath, "Documents", "report.txt"), "local content");
                }

                var source = new MirrorPulseJournalUploadSource(
                    feed, router, catalog, _ => run != 1);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                MirrorPulseJournalUploadBatch batch = await source.ReadPendingAsync(timeout.Token);
                CloudLocalChangeBatch raw = await feed.ReadBatchAsync(timeout.Token);
                Assert.IsFalse(raw.Changes.Any(change => change.RelativePath == "Documents"));
                Assert.IsFalse(batch.RequiresFullRescan);
                if (run == 0)
                {
                    Assert.IsNotEmpty(batch.ReadyCommands);
                    originalId = batch.ReadyCommands[0].OperationId;
                    Assert.AreNotEqual(Guid.Empty, originalId);
                }
                else if (run == 1)
                {
                    Assert.IsEmpty(batch.ReadyCommands);
                    Assert.IsGreaterThan(0, batch.DeferredCount);
                    MirrorPulseWorkerRequestRecord? recorded = await catalog.ReadWorkerRequestAsync(originalId);
                    Assert.IsNotNull(recorded);
                    Assert.AreEqual(instance, recorded.InstanceId);
                }
                else
                {
                    Assert.IsNotEmpty(batch.ReadyCommands);
                    Assert.AreEqual(originalId, batch.ReadyCommands[0].OperationId);
                    Assert.AreEqual(0, batch.DeferredCount);
                }
            }
        }
        finally
        {
            cloud.Unregister(paths.SyncRootPath);
            Directory.Delete(root, recursive: true);
        }
    }
}

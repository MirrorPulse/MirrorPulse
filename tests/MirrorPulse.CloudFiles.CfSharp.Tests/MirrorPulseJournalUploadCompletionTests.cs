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
public sealed class MirrorPulseJournalUploadCompletionTests
{
    [TestMethod]
    [DoNotParallelize]
    public async Task NativeFailureRetryAndSuccessUseCfSharpJournalAcrossRestart()
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
        var policy = new BackoffPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5));
        try
        {
            cloud.Register(definition);
            Guid operationId = Guid.Empty;
            for (int run = 0; run < 2; run++)
            {
                var state = new MirrorPulseCfSharpStateSession(paths);
                await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths)
                    .WithStateStore(state)
                    .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                    .Build();
                await fileSystem.StartAsync();
                CloudLocalChangeFeed feed = fileSystem.CreateLocalChangeFeed();
                await feed.StartAsync();
                await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
                var completion = new MirrorPulseJournalUploadCompletion(feed, state, policy);
                var source = new MirrorPulseJournalUploadSource(feed, router, catalog, _ => true, completion);

                if (run == 0)
                {
                    await feed.SuppressProviderEchoAsync(
                        CloudStateOperationKind.MetadataUpdate,
                        "Documents",
                        DateTimeOffset.UtcNow.AddMinutes(1));
                    await fileSystem.Root.CreatePlaceholdersAsync(router.CreateRootPage().Children);
                    await File.WriteAllTextAsync(
                        Path.Combine(paths.SyncRootPath, "Documents", "report.txt"), "local change");
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    MirrorPulseJournalUploadBatch pending = await source.ReadPendingAsync(timeout.Token);
                    Assert.IsNotEmpty(pending.ReadyCommands);
                    operationId = pending.ReadyCommands[0].OperationId;
                    RetryAfterDirective retry = await completion.DeferFailedUploadAsync(
                        operationId, DateTimeOffset.UtcNow);
                    Assert.AreEqual(1, retry.Attempt);
                    Assert.AreEqual(TimeSpan.FromMinutes(1), retry.Delay);
                    MirrorPulseJournalUploadBatch delayed = await source.ReadPendingAsync();
                    Assert.IsFalse(delayed.ReadyCommands.Any(command => command.OperationId == operationId));
                    Assert.IsGreaterThan(0, delayed.DeferredCount);
                }
                else
                {
                    DateTimeOffset? retryAfter = await completion.GetRetryAfterAsync(operationId);
                    Assert.IsNotNull(retryAfter);
                    Assert.IsGreaterThan(DateTimeOffset.UtcNow, retryAfter.Value);
                    MirrorPulseJournalUploadBatch delayed = await source.ReadPendingAsync();
                    Assert.IsFalse(delayed.ReadyCommands.Any(command => command.OperationId == operationId));
                    await completion.AcknowledgeSuccessfulUploadAsync(operationId, "remote-revision-1");
                    await using ICloudStateTransaction transaction = await state.OpenStore.BeginTransactionAsync();
                    Assert.IsNull(await transaction.Operations.GetAsync(operationId));
                    await transaction.RollbackAsync();
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

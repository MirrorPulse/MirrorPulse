using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseLocalBatchMapperTests
{
    [TestMethod]
    public async Task NativeJournalBatchRetainsOperationIdentityWithoutContentPayload()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var definition = new MirrorPulseSyncRootDefinition(
            paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]);
        var cloud = new CfSharpMirrorPulseCloudRootRegistry();
        Directory.CreateDirectory(Path.Combine(paths.SyncRootPath, "Documents"));
        try
        {
            cloud.Register(definition);
            await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
                .Build();
            await fileSystem.StartAsync();
            CloudLocalChangeFeed feed = fileSystem.CreateLocalChangeFeed();
            await feed.StartAsync();
            string file = Path.Combine(paths.SyncRootPath, "Documents", "report.txt");
            await File.WriteAllTextAsync(file, "content stays in the local file");

            CloudLocalChangeBatch batch = await WaitForFileChangeAsync(feed, "Documents", "report.txt");
            var instance = InstanceId.New();
            RootRegistration registration = AdapterRootRegistrationMapper.Map(
                AdapterId.Parse("example.local"), instance,
                new AdapterRootDefinition("docs", "Documents", "Documents", false),
                RootRegistrationState.Active);
            var router = new MirrorPulseRootRouter(paths.SyncRootPath, [registration]);
            MirrorPulseLocalBatchPlan plan = MirrorPulseLocalBatchMapper.Map(batch, router);
            Assert.IsFalse(plan.RequiresFullRescan);
            Assert.IsTrue(plan.Commands.Any(command =>
                command.InstanceId == instance &&
                command.RootKey == "docs" &&
                command.RelativePath == "report.txt" &&
                command.OperationId != Guid.Empty));

            CloudLocalChangeBatch repeated = await feed.ReadBatchAsync();
            var repeatedPlan = MirrorPulseLocalBatchMapper.Map(repeated, router);
            CollectionAssert.AreEqual(
                plan.Commands.Select(command => command.OperationId).ToArray(),
                repeatedPlan.Commands.Select(command => command.OperationId).ToArray());
            Assert.IsFalse(typeof(MirrorPulseWorkerChangeCommand).GetProperties()
                .Any(property => property.PropertyType == typeof(byte[])
                    || property.Name.Contains("Payload", StringComparison.Ordinal)));
        }
        finally
        {
            cloud.Unregister(paths.SyncRootPath);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<CloudLocalChangeBatch> WaitForFileChangeAsync(
        CloudLocalChangeFeed feed,
        string directoryName,
        string fileName)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            timeout.Token.ThrowIfCancellationRequested();
            CloudLocalChangeBatch batch = await feed.ReadBatchAsync(timeout.Token);
            if (batch.Changes.Any(change =>
                change.RelativePath.EndsWith(
                    Path.Combine(directoryName, fileName),
                    StringComparison.OrdinalIgnoreCase)))
            {
                return batch;
            }

            await Task.Delay(100, timeout.Token);
        }
    }
}

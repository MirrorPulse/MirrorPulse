using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
public sealed class MirrorPulseRemoteConflictProjectorTests
{
    [TestMethod]
    public async Task OfficialConflictAuthorityRebuildsProductProjectionAfterRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);
        Guid conflictId = Guid.NewGuid();
        var instance = InstanceId.New();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        try
        {
            var firstState = new MirrorPulseCfSharpStateSession(paths);
            await using (ICloudStateStore official = await firstState.OpenAsync(
                new CloudStateStoreContext(paths.SyncRootPath)))
            await using (var catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                await using (ICloudStateTransaction transaction = await official.BeginTransactionAsync())
                {
                    await transaction.Conflicts.UpsertAsync(new CloudConflictState(
                        conflictId, null, CloudStateConflictKind.Metadata, new byte[] { 1 }, now));
                    await transaction.CommitAsync();
                }

                var center = new MirrorPulseConflictCenter();
                int notifications = 0;
                var projector = new MirrorPulseRemoteConflictProjector(
                    firstState, catalog, center,
                    new MirrorPulseConflictNotificationBridge((_, _) =>
                    {
                        notifications++;
                        return ValueTask.CompletedTask;
                    }));
                var change = new CloudRemoteChange(
                    "change-1", CloudRemoteChangeKind.DirectoryUpsert, "remote-1", "revision-2",
                    CloudItemKind.Directory, "Docs",
                    metadata: CloudPlaceholderMetadata.CreateDirectoryBuilder().Build());
                await projector.CaptureAsync(instance, conflictId,
                    new CloudRemoteConflict(change, null, CloudRemoteConflictReason.PathCollision, now));
                await projector.CaptureAsync(instance, conflictId,
                    new CloudRemoteConflict(change, null, CloudRemoteConflictReason.PathCollision, now));
                Assert.AreEqual(1, notifications);
                MirrorPulseConflictRecord projected = center.Query().Single();
                Assert.AreEqual(MirrorPulseConflictSource.CfSharpRemote, projected.Source);
                Assert.AreEqual(MirrorPulseConflictReason.PathCollision, projected.Reason);
                Assert.AreEqual(conflictId, projected.ConflictId);
                Assert.AreEqual(instance, projected.InstanceId);
            }

            var secondState = new MirrorPulseCfSharpStateSession(paths);
            await using (ICloudStateStore official = await secondState.OpenAsync(
                new CloudStateStoreContext(paths.SyncRootPath)))
            await using (var catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                var center = new MirrorPulseConflictCenter();
                Guid uploadId = Guid.NewGuid();
                center.Upsert(new MirrorPulseConflictRecord(
                    uploadId, instance, "upload-1", "upload.txt", MirrorPulseConflictReason.Content,
                    MirrorPulse.Core.Sync.MirrorPulseVersionComparison.Unknown, null, null, now));
                var projector = new MirrorPulseRemoteConflictProjector(
                    secondState, catalog, center,
                    new MirrorPulseConflictNotificationBridge((_, _) => ValueTask.CompletedTask));
                Assert.IsEmpty(await projector.RestoreAsync());
                Assert.HasCount(2, center.Query());
                Assert.AreEqual(MirrorPulseConflictSource.Upload,
                    center.Query().Single(record => record.ConflictId == uploadId).Source);
                Assert.AreEqual(MirrorPulseConflictSource.CfSharpRemote,
                    center.Query().Single(record => record.ConflictId == conflictId).Source);

                await using (ICloudStateTransaction transaction = await official.BeginTransactionAsync())
                {
                    await transaction.Conflicts.RemoveAsync(conflictId);
                    await transaction.CommitAsync();
                }

                Assert.IsEmpty(await projector.RestoreAsync());
                Assert.AreEqual(uploadId, center.Query().Single().ConflictId);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

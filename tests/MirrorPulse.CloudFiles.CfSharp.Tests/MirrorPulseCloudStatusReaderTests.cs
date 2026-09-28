using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
public sealed class MirrorPulseCloudStatusReaderTests
{
    [TestMethod]
    public async Task ReadsOfficialJournalAndOpaqueCursorAfterStoreRestart()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var instance = InstanceId.New();
        byte[] cursor = [5, 6, 7];
        try
        {
            Directory.CreateDirectory(paths.SyncRootPath);
            ICloudStateStoreFactory factory = MirrorPulseCfSharpStateStoreFactory.Create(paths);
            await using (ICloudStateStore store = await factory.OpenAsync(
                new CloudStateStoreContext(paths.SyncRootPath)))
            {
                await using ICloudStateTransaction transaction = await store.BeginTransactionAsync();
                await transaction.Operations.EnqueueAsync(new CloudOperationJournalEntry(
                    Guid.NewGuid(), CloudStateOperationKind.MetadataUpdate, null, [1],
                    DateTimeOffset.UtcNow, 0, null, 0));
                await transaction.Checkpoints.UpsertAsync(new CloudStateCheckpoint(
                    MirrorPulseRemoteBatchCoordinator.GetCheckpointName(instance), cursor,
                    DateTimeOffset.UtcNow));
                await transaction.CommitAsync();
            }

            await using ICloudStateStore reopened = await factory.OpenAsync(
                new CloudStateStoreContext(paths.SyncRootPath));
            MirrorPulseCloudStatusSnapshot snapshot = await MirrorPulseCloudStatusReader.ReadAsync(reopened, [instance]);
            Assert.AreEqual(1, snapshot.PendingUploadCount);
            Assert.AreEqual(0, snapshot.PendingRemoteConflictCount);
            Assert.HasCount(1, snapshot.Cursors);
            Assert.IsTrue(snapshot.Cursors[0].HasCursor);
            Assert.AreEqual(12, snapshot.Cursors[0].CursorFingerprint?.Length);
            Assert.AreNotEqual(Convert.ToBase64String(cursor), snapshot.Cursors[0].CursorFingerprint);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

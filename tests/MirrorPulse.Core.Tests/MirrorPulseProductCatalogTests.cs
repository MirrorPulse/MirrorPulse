using System.Security.Cryptography;
using System.Text;
using CfSharp;
using CfSharp.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseProductCatalogTests
{
    [TestMethod]
    public async Task ProductMetadataReopensSeparatelyFromOfficialCfSharpState()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);
        var operationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var instance = InstanceId.New();
        byte[] fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes("stable Worker request"));
        try
        {
            var cfSharpFactory = new SqliteCloudStateStoreFactory(paths.CfSharpStateDatabasePath);
            await using (ICloudStateStore cfSharp = await cfSharpFactory.OpenAsync(
                new CloudStateStoreContext(paths.SyncRootPath)))
            {
                await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
                Assert.IsTrue(await catalog.TryRecordWorkerRequestAsync(operationId, instance, fingerprint));
                Assert.IsFalse(await catalog.TryRecordWorkerRequestAsync(operationId, instance, fingerprint));
                await catalog.SaveUserCommandAsync(new(commandId, "keep-both", "conflict-1", "pending"));
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                    catalog.TryRecordWorkerRequestAsync(
                        operationId,
                        instance,
                        SHA256.HashData(Encoding.UTF8.GetBytes("different request"))));
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                    catalog.SaveUserCommandAsync(new(commandId, "delete", "conflict-1", "pending")));

                await Assert.ThrowsExactlyAsync<IOException>(() => MirrorPulseProductCatalog.OpenAsync(paths));
            }

            await using (var reopened = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                MirrorPulseWorkerRequestRecord? worker = await reopened.ReadWorkerRequestAsync(operationId);
                Assert.IsNotNull(worker);
                Assert.AreEqual(instance, worker.InstanceId);
                CollectionAssert.AreEqual(fingerprint, worker.Fingerprint);
                Assert.AreEqual(0, worker.Attempt);
                MirrorPulseUserCommandRecord? userCommand = await reopened.ReadUserCommandAsync(commandId);
                Assert.IsNotNull(userCommand);
                Assert.AreEqual("keep-both", userCommand.Action);
                Assert.AreEqual("pending", userCommand.State);
            }

            Assert.AreNotEqual(paths.ProductCatalogDatabasePath, paths.CfSharpStateDatabasePath);
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = paths.CfSharpStateDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString()))
            {
                await connection.OpenAsync();
                await using SqliteCommand tables = connection.CreateCommand();
                tables.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('worker_requests', 'user_commands');";
                Assert.AreEqual(0L, await tables.ExecuteScalarAsync());
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

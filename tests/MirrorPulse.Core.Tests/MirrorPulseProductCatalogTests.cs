using System.Security.Cryptography;
using System.Text;
using CfSharp;
using CfSharp.Storage.Sqlite;
using Microsoft.Data.Sqlite;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseProductCatalogTests
{
    [TestMethod]
    public async Task SnoozedConflictNotificationSurvivesRestartAndCanBeRestored()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Guid conflictId = Guid.NewGuid();
        try
        {
            await using (var catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                await catalog.SetRemoteConflictSnoozedAsync(conflictId, true);
                Assert.Contains(conflictId, await catalog.ReadSnoozedRemoteConflictIdsAsync());
            }

            await using (var reopened = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                Assert.Contains(conflictId, await reopened.ReadSnoozedRemoteConflictIdsAsync());
                await reopened.SetRemoteConflictSnoozedAsync(conflictId, false);
                Assert.DoesNotContain(conflictId, await reopened.ReadSnoozedRemoteConflictIdsAsync());
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task AdapterTopologyReopensAndRejectsCollidingRootWithoutChangingStoredInventory()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        AdapterId adapterId = AdapterId.Parse("example.drive");
        var installId = InstallId.New();
        var instanceId = InstanceId.New();
        var manifest = new AdapterManifest(1, adapterId, "Example", "1.0.0",
            new ProtocolVersionRange(1, 1), new Dictionary<string, string>
            {
                ["win-x64"] = "worker/win-x64/adapter.exe",
                ["win-arm64"] = "worker/win-arm64/adapter.exe",
            }, new AdapterInstallPolicy(null), new AdapterInstancePolicy(null, null),
            new AdapterCapabilities(true, false, true, true), ["en-US"], "1.0.0");
        var installation = new InstalledAdapter(manifest, installId, Path.Combine(root, "installed"),
            new Sha256Digest(new string('A', 64)), AdapterInstallSource.LocalFile, null,
            true, DateTimeOffset.UtcNow, AdapterLifecycleState.Installed);
        var instance = new AdapterInstance(adapterId, installId, instanceId, "Personal drive",
            new Dictionary<string, string> { ["server"] = "example.test" }, ["credential-1"],
            Path.Combine(root, "file-cache"), Path.Combine(root, "transfer-cache"), true,
            AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
        RootRegistration first = AdapterRootRegistrationMapper.Map(adapterId, instanceId,
            new AdapterRootDefinition("files", "Files", "Files", false), RootRegistrationState.Active);
        var secondInstallId = InstallId.New();
        var secondInstanceId = InstanceId.New();
        var newerManifest = new AdapterManifest(1, adapterId, "Example", "2.0.0",
            manifest.Protocol, manifest.Entrypoints, manifest.InstallPolicy, manifest.InstancePolicy,
            manifest.Capabilities, manifest.Locales, manifest.MinimumMirrorPulseVersion);
        var newerInstallation = new InstalledAdapter(newerManifest, secondInstallId,
            Path.Combine(root, "installed-newer"), new Sha256Digest(new string('B', 64)),
            AdapterInstallSource.LocalFile, null, true, DateTimeOffset.UtcNow, AdapterLifecycleState.Installed);
        var secondInstance = new AdapterInstance(adapterId, secondInstallId, secondInstanceId, "Backup",
            new Dictionary<string, string>(), [], Path.Combine(root, "files-2"),
            Path.Combine(root, "transfer-2"), true, AdapterLifecycleState.Enabled, null, DateTimeOffset.UtcNow);
        RootRegistration secondRoot = AdapterRootRegistrationMapper.Map(adapterId, secondInstanceId,
            new AdapterRootDefinition("backup", "Backup", "Backup", false), RootRegistrationState.Active);
        var topology = new MirrorPulseAdapterTopology(
            [installation, newerInstallation], [instance, secondInstance], [first, secondRoot]);

        try
        {
            MirrorPulseAdapterTopology beforeOpen = await MirrorPulseProductCatalog
                .ReadAdapterTopologySnapshotAsync(paths);
            Assert.IsEmpty(beforeOpen.Installations);
            await using (var catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                await catalog.SaveAdapterTopologyAsync(topology);
                MirrorPulseAdapterTopology whileHostOwnsCatalog = await MirrorPulseProductCatalog
                    .ReadAdapterTopologySnapshotAsync(paths);
                Assert.HasCount(2, whileHostOwnsCatalog.Installations);
                Assert.AreEqual(installId, whileHostOwnsCatalog.Installations[0].InstallId);
                RootRegistration duplicate = AdapterRootRegistrationMapper.Map(adapterId, instanceId,
                    new AdapterRootDefinition("other", "files", "files", false), RootRegistrationState.Disabled);
                await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                    catalog.SaveAdapterTopologyAsync(new(
                        [installation, newerInstallation], [instance, secondInstance],
                        [first, secondRoot, duplicate])));
                MirrorPulseAdapterTopology disabled = await catalog.SetInstanceEnabledAsync(instanceId, false);
                Assert.IsFalse(disabled.Instances[0].Enabled);
                Assert.AreEqual(RootRegistrationState.Disabled, disabled.Roots[0].State);
                Assert.IsTrue(disabled.Instances[1].Enabled);
                Assert.AreEqual(RootRegistrationState.Active, disabled.Roots[1].State);
                MirrorPulseAdapterTopology switched = await catalog.SelectInstanceInstallationAsync(
                    instanceId, secondInstallId);
                Assert.AreEqual(secondInstallId, switched.Instances[0].InstallId);
                Assert.AreEqual(instanceId, switched.Roots[0].InstanceId);
            }

            await using var reopened = await MirrorPulseProductCatalog.OpenAsync(paths);
            MirrorPulseAdapterTopology restored = await reopened.ReadAdapterTopologyAsync();
            MirrorPulseAdapterTopology snapshot = await MirrorPulseProductCatalog.ReadAdapterTopologySnapshotAsync(paths);
            Assert.HasCount(2, restored.Installations);
            Assert.HasCount(2, restored.Instances);
            Assert.HasCount(2, restored.Roots);
            Assert.AreEqual("example.drive", restored.Installations[0].AdapterId.ToString());
            Assert.AreEqual("Personal drive", restored.Instances[0].DisplayName);
            Assert.IsFalse(restored.Instances[0].Enabled);
            Assert.AreEqual(secondInstallId, restored.Instances[0].InstallId);
            Assert.AreEqual(RootRegistrationState.Disabled, restored.Roots[0].State);
            Assert.AreEqual("credential-1", restored.Instances[0].CredentialReferences[0]);
            Assert.AreEqual(first.RootId, restored.Roots[0].RootId);
            Assert.AreEqual(first.RootId, snapshot.Roots[0].RootId);
            Assert.IsNotNull(await reopened.FindAsync(installId));
            Assert.AreEqual("2.0.0", (await reopened.FindAsync(secondInstallId))?.Version);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task VersionOneCatalogUpgradesWithoutLosingWorkerFingerprint()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(Path.GetDirectoryName(paths.ProductCatalogDatabasePath)!);
        Guid operationId = Guid.NewGuid();
        var instance = InstanceId.New();
        byte[] fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes("legacy request"));
        try
        {
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = paths.ProductCatalogDatabasePath,
                Pooling = false,
            }.ToString()))
            {
                await connection.OpenAsync();
                await using SqliteCommand legacy = connection.CreateCommand();
                legacy.CommandText = """
                    CREATE TABLE worker_requests (
                        operation_id TEXT PRIMARY KEY,
                        instance_id TEXT NOT NULL,
                        fingerprint BLOB NOT NULL,
                        attempt INTEGER NOT NULL DEFAULT 0,
                        next_attempt_utc TEXT NULL
                    );
                    PRAGMA user_version=1;
                    INSERT INTO worker_requests (operation_id, instance_id, fingerprint)
                    VALUES ($operation, $instance, $fingerprint);
                    """;
                legacy.Parameters.AddWithValue("$operation", operationId.ToString("D"));
                legacy.Parameters.AddWithValue("$instance", instance.ToString());
                legacy.Parameters.AddWithValue("$fingerprint", fingerprint);
                await legacy.ExecuteNonQueryAsync();
            }

            await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
            MirrorPulseWorkerRequestRecord? restored = await catalog.ReadWorkerRequestAsync(operationId);
            Assert.IsNotNull(restored);
            CollectionAssert.AreEqual(fingerprint, restored.Fingerprint);
            await catalog.SaveInstanceRuntimeStateAsync(new(instance, "starting", true, null));
            Assert.IsNotNull(await catalog.ReadInstanceRuntimeStateAsync(instance));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task ProductMetadataReopensSeparatelyFromOfficialCfSharpState()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);
        var operationId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var instance = InstanceId.New();
        var runtime = new MirrorPulseInstanceRuntimeState(instance, "recovering", true, DateTimeOffset.UtcNow);
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
                await catalog.SaveInstanceRuntimeStateAsync(runtime);
                IReadOnlyList<MirrorPulseInstanceRuntimeState> liveRuntime = await MirrorPulseProductCatalog
                    .ReadRuntimeSnapshotAsync(paths);
                Assert.HasCount(1, liveRuntime);
                Assert.AreEqual(runtime, liveRuntime[0]);
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
                MirrorPulseInstanceRuntimeState? restored = await reopened.ReadInstanceRuntimeStateAsync(instance);
                Assert.IsNotNull(restored);
                Assert.AreEqual(runtime, restored);
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

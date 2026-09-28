using System.Runtime.Versioning;
using CfSharp;
using CfSharp.Storage.Sqlite;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseCloudFileSystemBuilderTests
{
    [TestMethod]
    public async Task BuilderCreatesInactiveCloudFileSystemWithoutOpeningStateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);

        try
        {
            var options = SyncRootRegistrationOptions.CreateBuilder("MirrorPulse", "0.1.0")
                .WithProviderId(Guid.Parse("7b6a7d8e-63a5-4b8a-9c6e-1e6e61f95e9d"))
                .WithSyncRootIdentity([1, 2, 3])
                .Build();
            var fileSystem = new MirrorPulseCloudFileSystemBuilder(path)
                .WithStateStore(new ThrowingStateStoreFactory())
                .WithRegistration(options)
                .Build();

            Assert.AreEqual(path, fileSystem.SyncRootPath);
            Assert.AreEqual(CloudFileSystemLifecycleState.Created, fileSystem.LifecycleState);
            await fileSystem.DisposeAsync();
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public void BuilderRequiresDurableStateStore()
    {
        var path = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        try
        {
            Assert.ThrowsExactly<InvalidOperationException>(() => new MirrorPulseCloudFileSystemBuilder(path).Build());
        }
        finally
        {
            Directory.Delete(path, recursive: true);
        }
    }

    [TestMethod]
    public async Task StoragePathsUseOfficialSqliteStoreAcrossReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);

        try
        {
            await using (var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths).Build())
            {
                Assert.AreEqual(CloudFileSystemLifecycleState.Created, fileSystem.LifecycleState);
            }

            var factory = MirrorPulseCfSharpStateStoreFactory.Create(paths);
            var context = new CloudStateStoreContext(paths.SyncRootPath);
            await using (var store = await factory.OpenAsync(context))
            {
                await using var transaction = await store.BeginTransactionAsync();
                await transaction.Checkpoints.UpsertAsync(
                    new CloudStateCheckpoint("mirrorpulse/test", [1, 2, 3], DateTimeOffset.UtcNow));
                await transaction.CommitAsync();

                var owned = await Assert.ThrowsExactlyAsync<SqliteCloudStateStoreException>(async () =>
                {
                    await factory.OpenAsync(context);
                });
                Assert.AreEqual(SqliteCloudStateStoreError.AlreadyInUse, owned.Error);
            }

            await using (var reopened = await factory.OpenAsync(context))
            {
                await using var transaction = await reopened.BeginTransactionAsync();
                var checkpoint = await transaction.Checkpoints.GetAsync("mirrorpulse/test");
                Assert.IsNotNull(checkpoint);
                CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, checkpoint.Value.ToArray());
                await transaction.RollbackAsync();
            }

            var insideRoot = new SqliteCloudStateStoreFactory(Path.Combine(paths.SyncRootPath, "unsafe.db"));
            var rejected = await Assert.ThrowsExactlyAsync<SqliteCloudStateStoreException>(async () =>
            {
                await insideRoot.OpenAsync(context);
            });
            Assert.AreEqual(SqliteCloudStateStoreError.PathInsideSyncRoot, rejected.Error);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class ThrowingStateStoreFactory : ICloudStateStoreFactory
    {
        public ValueTask<ICloudStateStore> OpenAsync(CloudStateStoreContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The builder test must not open the durable state store.");
    }
}

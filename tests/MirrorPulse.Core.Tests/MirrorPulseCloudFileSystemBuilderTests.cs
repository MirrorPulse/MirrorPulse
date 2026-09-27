using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

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

    private sealed class ThrowingStateStoreFactory : ICloudStateStoreFactory
    {
        public ValueTask<ICloudStateStore> OpenAsync(CloudStateStoreContext context, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The builder test must not open the durable state store.");
    }
}

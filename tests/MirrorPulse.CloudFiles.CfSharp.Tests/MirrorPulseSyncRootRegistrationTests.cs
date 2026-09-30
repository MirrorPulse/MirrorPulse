using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
public sealed class MirrorPulseSyncRootRegistrationTests
{
    [TestMethod]
    public void RegistrationServiceBuildsMirrorPulseIdentityAndUpdatesExistingRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var registrar = new RecordingRegistrar();
        var service = new MirrorPulseSyncRootRegistrationService(registrar);
        var providerId = Guid.Parse("7b6a7d8e-63a5-4b8a-9c6e-1e6e61f95e9d");

        var result = service.Register(new MirrorPulseSyncRootDefinition(path, "0.1.0", providerId, [1, 2, 3]));

        Assert.AreEqual(Path.GetFullPath(path), result.Path);
        Assert.AreEqual(providerId, registrar.Options!.ProviderId);
        Assert.AreEqual("MirrorPulse", registrar.Options.ProviderName);
        Assert.IsTrue(registrar.Options.UpdateExisting);
        Assert.IsTrue(registrar.Options.MarkRootInSync);
        Assert.AreEqual(CloudHydrationPolicy.Full, registrar.Options.HydrationPolicy);
        Assert.AreEqual(CloudHydrationPolicyModifiers.None, registrar.Options.HydrationModifiers);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, registrar.Options.SyncRootIdentity.ToArray());
        Assert.IsTrue(Directory.Exists(path));
        Directory.Delete(path, recursive: true);
    }

    private sealed class RecordingRegistrar : IMirrorPulseSyncRootRegistrar
    {
        public SyncRootRegistrationOptions? Options { get; private set; }

        public MirrorPulseSyncRootRegistrationResult Register(string path, SyncRootRegistrationOptions options)
        {
            Options = options;
            return new(path, options.ProviderId, options.ProviderName, options.ProviderVersion);
        }
    }
}

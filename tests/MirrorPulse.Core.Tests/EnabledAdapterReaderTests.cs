using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class EnabledAdapterReaderTests
{
    private static readonly string[] Locales = ["en-US"];

    [TestMethod]
    public async Task ReaderResolvesEnabledInstallationsInConfigurationOrder()
    {
        var first = CreateInstalledAdapter();
        var second = CreateInstalledAdapter();
        var configuration = new MirrorPulseConfiguration(
            MirrorPulseConfiguration.CurrentSchemaVersion,
            "en-US",
            false,
            false,
            new[] { first.InstallId, second.InstallId });
        var reader = new EnabledAdapterReader(new FakeCatalog(first, second));

        var result = await reader.ReadAsync(configuration);

        Assert.HasCount(2, result.Adapters);
        Assert.AreEqual(first.InstallId, result.Adapters[0].InstallId);
        Assert.HasCount(0, result.MissingInstallations);
    }

    [TestMethod]
    public async Task ReaderReportsMissingInstallationWithoutBlockingOthers()
    {
        var installed = CreateInstalledAdapter();
        var missing = InstallId.New();
        var configuration = new MirrorPulseConfiguration(
            MirrorPulseConfiguration.CurrentSchemaVersion,
            "en-US",
            false,
            false,
            new[] { installed.InstallId, missing });
        var result = await new EnabledAdapterReader(new FakeCatalog(installed)).ReadAsync(configuration);

        Assert.HasCount(1, result.Adapters);
        Assert.AreEqual(missing, result.MissingInstallations.Single());
    }

    private static InstalledAdapter CreateInstalledAdapter() => new(
        new AdapterManifest(
            1,
            new AdapterId($"sample-{Guid.NewGuid():N}"),
            "MirrorPulse",
            "1.0.0",
            new ProtocolVersionRange(1, 1),
            new Dictionary<string, string> { ["win-x64"] = "worker.exe" },
            new AdapterInstallPolicy(1),
            new AdapterInstancePolicy(null, null),
            new AdapterCapabilities(true, true, true, false),
            Locales,
            "1.0.0"),
        InstallId.New(),
        "C:\\MirrorPulse\\adapters",
        Sha256Digest.Compute(new byte[] { 1 }),
        AdapterInstallSource.LocalFile,
        null,
        false,
        DateTimeOffset.UtcNow,
        AdapterLifecycleState.Installed);

    private sealed class FakeCatalog(params InstalledAdapter[] adapters) : IInstalledAdapterCatalog
    {
        private readonly IReadOnlyDictionary<InstallId, InstalledAdapter> _adapters = adapters.ToDictionary(adapter => adapter.InstallId);

        public ValueTask<InstalledAdapter?> FindAsync(InstallId installId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_adapters.GetValueOrDefault(installId));
    }
}

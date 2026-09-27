using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ManifestTests
{
    [TestMethod]
    public void ManifestV1RetainsEntrypointsPoliciesAndLocales()
    {
        var manifest = CreateManifest();

        Assert.AreEqual(1, manifest.SchemaVersion);
        Assert.AreEqual("example.webdav", manifest.AdapterId.Value);
        Assert.AreEqual("1.2.3", manifest.Version);
        Assert.AreEqual("payload/win-x64/Adapter.exe", manifest.Entrypoints["win-x64"]);
        Assert.IsNull(manifest.InstallPolicy.MaximumInstallations);
        Assert.AreEqual(8, manifest.InstancePolicy.MaximumRootDefinitions);
        CollectionAssert.Contains(manifest.Locales.ToArray(), "zh-CN");
    }

    [TestMethod]
    public void ManifestCopiesMutableInputCollections()
    {
        var entrypoints = new Dictionary<string, string> { ["win-x64"] = "payload/old.exe" };
        var locales = new List<string> { "en-US" };
        var manifest = new AdapterManifest(
            1,
            AdapterId.Parse("example.local"),
            "Example",
            "1.0.0",
            new ProtocolVersionRange(1, 1),
            entrypoints,
            new AdapterInstallPolicy(1),
            new AdapterInstancePolicy(1, 1),
            new AdapterCapabilities(false, true, false, false),
            locales,
            "1.0.0");

        entrypoints["win-x64"] = "payload/new.exe";
        locales.Add("zh-CN");

        Assert.AreEqual("payload/old.exe", manifest.Entrypoints["win-x64"]);
        Assert.HasCount(1, manifest.Locales);
    }

    private static AdapterManifest CreateManifest() => new(
        1,
        AdapterId.Parse("Example.WebDAV"),
        "Example Publisher",
        "1.2.3",
        new ProtocolVersionRange(1, 1),
        new Dictionary<string, string>
        {
            ["win-x64"] = "payload/win-x64/Adapter.exe",
            ["win-arm64"] = "payload/win-arm64/Adapter.exe"
        },
        new AdapterInstallPolicy(null),
        new AdapterInstancePolicy(null, 8),
        new AdapterCapabilities(true, true, true, true),
        ManifestLocales,
        "1.0.0");

    private static readonly string[] ManifestLocales = ["en-US", "zh-CN"];
}

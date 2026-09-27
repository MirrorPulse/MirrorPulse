using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterLocaleMetadataTests
{
    [TestMethod]
    public void LocaleCatalogResolvesRequestedLocaleAndFallsBackToEnglish()
    {
        var english = new AdapterLocaleMetadata("en-US", "English", "locales/en-US.json");
        var chinese = new AdapterLocaleMetadata("zh-CN", "简体中文", "locales/zh-CN.json");
        var catalog = new AdapterLocaleCatalog([english, chinese]);

        Assert.AreEqual(chinese, catalog.Resolve("zh-CN"));
        Assert.AreEqual(english, catalog.Resolve("fr-FR"));
    }

    [TestMethod]
    public void ManifestValidatorChecksLocaleMetadataAgainstDeclaredLocales()
    {
        var manifest = new AdapterManifest(
            1,
            AdapterId.Parse("example.webdav"),
            "Example Publisher",
            "1.0.0",
            new ProtocolVersionRange(1, 1),
            new Dictionary<string, string>
            {
                ["win-x64"] = "payload/win-x64/Adapter.exe",
                ["win-arm64"] = "payload/win-arm64/Adapter.exe"
            },
            new AdapterInstallPolicy(null),
            new AdapterInstancePolicy(null, null),
            new AdapterCapabilities(true, true, true, true),
            ["en-US"],
            "1.0.0",
            localeMetadata:
            [
                new AdapterLocaleMetadata("fr-FR", "Français", "locales/fr-FR.json")
            ]);

        var diagnostics = AdapterManifestValidator.Validate(manifest);

        CollectionAssert.Contains(diagnostics.Select(diagnostic => diagnostic.Code).ToArray(), "manifest.localeMetadata.undeclared");
        CollectionAssert.Contains(diagnostics.Select(diagnostic => diagnostic.Code).ToArray(), "manifest.localeMetadata.missingFallback");
    }
}

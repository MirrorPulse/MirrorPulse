using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterRootDefinitionTests
{
    [TestMethod]
    public void ManifestRetainsAdapterDeclaredRootDefinitions()
    {
        var root = new AdapterRootDefinition("documents", "Documents", "Documents", customEntry: false);
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
            new AdapterInstancePolicy(null, 2),
            new AdapterCapabilities(true, true, true, true),
            ["en-US"],
            "1.0.0",
            [root]);

        var diagnostics = AdapterManifestValidator.Validate(manifest);

        Assert.AreEqual(root, manifest.RootDefinitions.Single());
        Assert.IsFalse(diagnostics.Any(diagnostic => diagnostic.Code.StartsWith("manifest.rootDefinitions", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ManifestValidatorRejectsDuplicateRootKeys()
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
            new AdapterInstancePolicy(null, 2),
            new AdapterCapabilities(true, true, true, true),
            ["en-US"],
            "1.0.0",
            [
                new AdapterRootDefinition("documents", "Documents", "Documents", customEntry: false),
                new AdapterRootDefinition("documents", "Other", "Other", customEntry: false)
            ]);

        var diagnostics = AdapterManifestValidator.Validate(manifest);

        CollectionAssert.Contains(diagnostics.Select(diagnostic => diagnostic.Code).ToArray(), "manifest.rootDefinitions.duplicate");
    }
}

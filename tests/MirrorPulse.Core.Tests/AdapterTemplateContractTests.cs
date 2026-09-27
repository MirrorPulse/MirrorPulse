using System.Text.Json;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterTemplateContractTests
{
    private static readonly string TemplateRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../Adapters/template"));

    [TestMethod]
    public void TemplateManifestDeclaresSupportedRuntimePayloadsAndRoots()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(TemplateRoot, "manifest.json")));
        var root = manifest.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("com.example.mirrorpulse.adapter", root.GetProperty("adapterId").GetString());
        Assert.AreEqual("worker/win-x64/MirrorPulse.Adapter.Worker.exe", root.GetProperty("entrypoints").GetProperty("win-x64").GetString());
        Assert.AreEqual("worker/win-arm64/MirrorPulse.Adapter.Worker.exe", root.GetProperty("entrypoints").GetProperty("win-arm64").GetString());
        Assert.AreEqual("Documents", root.GetProperty("rootDefinitions")[0].GetProperty("label").GetString());
        Assert.AreEqual("locales/en-US.json", root.GetProperty("localeMetadata").GetProperty("en-US").GetProperty("resourcePath").GetString());
    }

    [TestMethod]
    public void PackagedTemplateContainsReferencedResources()
    {
        var packageRoot = Path.Combine(TemplateRoot, "package");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(packageRoot, "manifest.json")));
        var entrypoints = manifest.RootElement.GetProperty("entrypoints");

        Assert.IsTrue(File.Exists(Path.Combine(packageRoot, "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(packageRoot, "locales/en-US.json")));
        Assert.IsTrue(Directory.Exists(Path.Combine(packageRoot, "worker/win-x64")));
        Assert.IsTrue(Directory.Exists(Path.Combine(packageRoot, "worker/win-arm64")));
        StringAssert.Contains(entrypoints.GetProperty("win-x64").GetString()!, "worker/win-x64/");
        StringAssert.Contains(entrypoints.GetProperty("win-arm64").GetString()!, "worker/win-arm64/");
    }
}

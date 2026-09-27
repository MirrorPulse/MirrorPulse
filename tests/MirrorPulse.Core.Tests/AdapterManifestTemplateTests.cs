using System.Text.Json;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterManifestTemplateTests
{
    [TestMethod]
    public void ManifestTemplateContainsBothRuntimeEntrypoints()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Adapters", "README.md")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository);
        var path = Path.Combine(repository.FullName, "Adapters", "template", "manifest.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;

        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.IsTrue(root.GetProperty("entrypoints").TryGetProperty("win-x64", out _));
        Assert.IsTrue(root.GetProperty("entrypoints").TryGetProperty("win-arm64", out _));
        Assert.AreEqual("com.example.mirrorpulse.adapter", root.GetProperty("adapterId").GetString());
    }
}

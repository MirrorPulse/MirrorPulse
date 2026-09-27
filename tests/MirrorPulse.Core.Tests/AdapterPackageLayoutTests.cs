using System.Text.Json;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterPackageLayoutTests
{
    [TestMethod]
    public void PackageTemplateContainsManifestWorkersAndLocaleResources()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Adapters", "README.md")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository);
        var package = Path.Combine(repository.FullName, "Adapters", "template", "package");
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(package, "manifest.json")));

        Assert.AreEqual(1, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.IsTrue(Directory.Exists(Path.Combine(package, "worker", "win-x64")));
        Assert.IsTrue(Directory.Exists(Path.Combine(package, "worker", "win-arm64")));
        Assert.IsTrue(File.Exists(Path.Combine(package, "locales", "en-US.json")));
    }
}

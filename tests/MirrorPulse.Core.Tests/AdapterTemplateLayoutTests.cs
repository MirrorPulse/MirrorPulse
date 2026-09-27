namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterTemplateLayoutTests
{
    [TestMethod]
    public void AdapterTemplateHasIndependentRepositoryBoundaries()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Adapters", "README.md")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository);
        var root = Path.Combine(repository.FullName, "Adapters");

        Assert.IsTrue(File.Exists(Path.Combine(root, "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(root, ".gitignore")));
        Assert.IsTrue(Directory.Exists(Path.Combine(root, "src")));
        Assert.IsTrue(Directory.Exists(Path.Combine(root, "samples")));
        Assert.IsTrue(Directory.Exists(Path.Combine(root, "template")));
        Assert.IsTrue(Directory.Exists(Path.Combine(root, "eng")));
    }
}

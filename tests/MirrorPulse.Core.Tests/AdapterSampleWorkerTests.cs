namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterSampleWorkerTests
{
    [TestMethod]
    public void SampleWorkerProjectDefinesExecutableEntryPoint()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "Adapters", "README.md")))
        {
            repository = repository.Parent;
        }

        Assert.IsNotNull(repository);
        var project = Path.Combine(repository.FullName, "Adapters", "samples", "MirrorPulse.Adapter.SampleWorker", "MirrorPulse.Adapter.SampleWorker.csproj");
        var source = File.ReadAllText(project);

        StringAssert.Contains(source, "<OutputType>Exe</OutputType>");
        Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs")));
    }
}

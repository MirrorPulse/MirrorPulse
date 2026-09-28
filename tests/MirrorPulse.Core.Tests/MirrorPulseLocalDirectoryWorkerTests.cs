using MirrorPulse.Core.Adapters.LocalDirectory;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDirectoryWorkerTests
{
    [TestMethod]
    public async Task WorkerStartsAndStopsAgainstAnExistingSourceDirectory()
    {
        var source = CreateDirectory();
        try
        {
            var worker = new MirrorPulseLocalDirectoryWorker(
                new MirrorPulseLocalDirectoryWorkerOptions(Guid.NewGuid(), source));

            worker.Start();
            Assert.AreEqual(MirrorPulseLocalDirectoryWorkerState.Running, worker.State);
            await worker.StopAsync();
            Assert.AreEqual(MirrorPulseLocalDirectoryWorkerState.Stopped, worker.State);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [TestMethod]
    public void WorkerRejectsAStartWhenTheSourceDirectoryIsMissing()
    {
        var worker = new MirrorPulseLocalDirectoryWorker(
            new MirrorPulseLocalDirectoryWorkerOptions(Guid.NewGuid(), Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));

        Assert.ThrowsExactly<DirectoryNotFoundException>(() => worker.Start());
        Assert.AreEqual(MirrorPulseLocalDirectoryWorkerState.Created, worker.State);
    }

    private static string CreateDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"mirrorpulse-local-worker-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

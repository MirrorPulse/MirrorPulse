using MirrorPulse.Core.Adapters.Smb;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSmbWorkerTests
{
    [TestMethod]
    public async Task WorkerTracksUncPathAndLifecycle()
    {
        var worker = new MirrorPulseSmbWorker(
            new MirrorPulseSmbWorkerOptions(Guid.NewGuid(), "\\\\server\\share\\documents"));

        worker.Start();
        Assert.AreEqual("\\\\server\\share\\documents", worker.Options.NetworkPath);
        Assert.AreEqual(MirrorPulseSmbWorkerState.Running, worker.State);
        await worker.StopAsync();
        Assert.AreEqual(MirrorPulseSmbWorkerState.Stopped, worker.State);
    }

    [TestMethod]
    public void WorkerRejectsNonUncPaths()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseSmbWorkerOptions(Guid.NewGuid(), "C:\\documents"));
    }
}

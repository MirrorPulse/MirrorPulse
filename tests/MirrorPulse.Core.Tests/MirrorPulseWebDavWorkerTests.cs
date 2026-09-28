using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavWorkerTests
{
    [TestMethod]
    public async Task WorkerTracksTheWebDavEndpointAndLifecycle()
    {
        var endpoint = new Uri("https://dav.example.test/files/");
        var worker = new MirrorPulseWebDavWorker(new MirrorPulseWebDavWorkerOptions(Guid.NewGuid(), endpoint));

        worker.Start();
        Assert.AreEqual(endpoint, worker.Options.BaseUri);
        Assert.AreEqual(MirrorPulseWebDavWorkerState.Running, worker.State);
        await worker.StopAsync();
        Assert.AreEqual(MirrorPulseWebDavWorkerState.Stopped, worker.State);
    }

    [TestMethod]
    public void WorkerRejectsNonHttpEndpoints()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseWebDavWorkerOptions(
            Guid.NewGuid(),
            new Uri("ftp://dav.example.test/files/")));
    }
}

using MirrorPulse.Core.Adapters.Ftp;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseFtpWorkerTests
{
    [TestMethod]
    public async Task WorkerTracksFtpEndpointAndExplicitTlsMode()
    {
        var endpoint = new Uri("ftp://files.example.test/shared/");
        var worker = new MirrorPulseFtpWorker(new MirrorPulseFtpWorkerOptions(
            Guid.NewGuid(), endpoint, MirrorPulseFtpSecurityMode.ExplicitTls));

        worker.Start();
        Assert.AreEqual(endpoint, worker.Options.ServerUri);
        Assert.AreEqual(MirrorPulseFtpSecurityMode.ExplicitTls, worker.Options.SecurityMode);
        Assert.AreEqual(MirrorPulseFtpWorkerState.Running, worker.State);
        await worker.StopAsync();
        Assert.AreEqual(MirrorPulseFtpWorkerState.Stopped, worker.State);
    }

    [TestMethod]
    public void WorkerRejectsEmbeddedCredentialsAndNonFtpEndpoints()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseFtpWorkerOptions(
            Guid.NewGuid(), new Uri("ftp://user:secret@files.example.test/"), MirrorPulseFtpSecurityMode.ExplicitTls));
        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseFtpWorkerOptions(
            Guid.NewGuid(), new Uri("https://files.example.test/"), MirrorPulseFtpSecurityMode.Plain));
    }
}

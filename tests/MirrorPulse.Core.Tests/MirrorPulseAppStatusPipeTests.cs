using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseAppStatusPipeTests
{
    [TestMethod]
    public async Task CurrentUserAppReceivesBoundedHostStatus()
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var expected = new MirrorPulseAppStatusResponse(3, 1,
            [new("instance-1", "Personal drive", true, "Healthy", "ABCDEF012345", null, null)]);
        var server = new MirrorPulseAppStatusPipe(_ => Task.FromResult(expected));
        Task serving = server.ServeAsync(shutdown.Token);
        try
        {
            MirrorPulseAppStatusResponse received = await MirrorPulseAppStatusPipe.RequestAsync(shutdown.Token);
            Assert.AreEqual(3, received.PendingUploads);
            Assert.AreEqual(1, received.PendingRemoteConflicts);
            Assert.HasCount(1, received.Instances);
            Assert.AreEqual("Personal drive", received.Instances[0].DisplayName);
        }
        finally
        {
            await shutdown.CancelAsync();
            await serving;
        }
    }
}

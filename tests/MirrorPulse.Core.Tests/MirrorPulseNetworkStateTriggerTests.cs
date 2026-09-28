using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseNetworkStateTriggerTests
{
    [TestMethod]
    public async Task TriggerDrainsOnlyOnOfflineToOnlineEdges()
    {
        var drainCount = 0;
        using var trigger = new MirrorPulseNetworkStateTrigger(
            initialOnline: false,
            _ =>
            {
                drainCount++;
                return ValueTask.CompletedTask;
            });

        var stillOffline = await trigger.ObserveAsync(false);
        var firstOnline = await trigger.ObserveAsync(true);
        var stillOnline = await trigger.ObserveAsync(true);
        var offlineAgain = await trigger.ObserveAsync(false);
        var secondOnline = await trigger.ObserveAsync(true);

        Assert.IsFalse(stillOffline.DrainRequested);
        Assert.IsTrue(firstOnline.DrainRequested);
        Assert.IsFalse(stillOnline.DrainRequested);
        Assert.IsFalse(offlineAgain.DrainRequested);
        Assert.IsTrue(secondOnline.DrainRequested);
        Assert.AreEqual(2, drainCount);
        Assert.IsTrue(trigger.IsOnline);
    }

    [TestMethod]
    public async Task TriggerPropagatesDrainCancellation()
    {
        using var trigger = new MirrorPulseNetworkStateTrigger(
            initialOnline: false,
            _ => new ValueTask(Task.FromCanceled(new CancellationToken(canceled: true))));

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
            trigger.ObserveAsync(true).AsTask());
    }
}

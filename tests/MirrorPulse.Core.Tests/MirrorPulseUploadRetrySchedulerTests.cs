using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseUploadRetrySchedulerTests
{
    [TestMethod]
    public void SchedulerUsesExponentialDelayAndCapsAtMaximum()
    {
        var scheduler = new MirrorPulseUploadRetryScheduler(
            new BackoffPolicy(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), multiplier: 2));
        var now = DateTimeOffset.UtcNow;

        var first = scheduler.Schedule(0, now, "network");
        var second = scheduler.Schedule(first.Attempt, now, "network");
        var third = scheduler.Schedule(second.Attempt, now, "network");

        Assert.AreEqual(TimeSpan.FromSeconds(2), first.Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(4), second.Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(5), third.Delay);
        Assert.AreEqual("network", first.Reason);
    }

    [TestMethod]
    public void SchedulerRejectsNegativeJournalAttempt()
    {
        var scheduler = new MirrorPulseUploadRetryScheduler(
            new BackoffPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            scheduler.Schedule(-1, DateTimeOffset.UtcNow));
    }
}

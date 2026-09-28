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
        var operation = MirrorPulseUploadOperationStateMachine.Start(Create());
        var now = DateTimeOffset.UtcNow;

        var first = scheduler.Schedule(operation, now, "network");
        var waitingAfterFirst = MirrorPulseUploadOperationStateMachine.MarkRetry(operation, first.RetryAt);
        var second = scheduler.Schedule(
            MirrorPulseUploadOperationStateMachine.Start(waitingAfterFirst),
            now,
            "network");
        var waitingAfterSecond = MirrorPulseUploadOperationStateMachine.MarkRetry(
            MirrorPulseUploadOperationStateMachine.Start(waitingAfterFirst),
            second.RetryAt);
        var third = scheduler.Schedule(
            MirrorPulseUploadOperationStateMachine.Start(waitingAfterSecond),
            now,
            "network");

        Assert.AreEqual(TimeSpan.FromSeconds(2), first.Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(4), second.Delay);
        Assert.AreEqual(TimeSpan.FromSeconds(5), third.Delay);
        Assert.AreEqual("network", first.Reason);
    }

    [TestMethod]
    public void SchedulerRejectsPendingOperations()
    {
        var scheduler = new MirrorPulseUploadRetryScheduler(
            new BackoffPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            scheduler.Schedule(Create(), DateTimeOffset.UtcNow));
    }

    private static MirrorPulseQueuedUpload Create() => new(
        Guid.NewGuid(),
        InstanceId.New(),
        MirrorPulseUploadOperationKind.Update,
        "file.txt",
        null,
        new byte[] { 1 },
        DateTimeOffset.UtcNow);
}

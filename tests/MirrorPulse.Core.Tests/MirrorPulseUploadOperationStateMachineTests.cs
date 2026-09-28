using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseUploadOperationStateMachineTests
{
    [TestMethod]
    public void StateMachineMovesPendingOperationThroughRetryToSuccess()
    {
        var operation = Create();
        var started = MirrorPulseUploadOperationStateMachine.Start(operation);
        var retryAt = DateTimeOffset.UtcNow.AddMinutes(1);
        var waiting = MirrorPulseUploadOperationStateMachine.MarkRetry(started, retryAt);
        var restarted = MirrorPulseUploadOperationStateMachine.Start(waiting);
        var succeeded = MirrorPulseUploadOperationStateMachine.Succeed(restarted);

        Assert.AreEqual(MirrorPulseUploadOperationState.InFlight, started.State);
        Assert.AreEqual(MirrorPulseUploadOperationState.WaitingToRetry, waiting.State);
        Assert.AreEqual(1, waiting.Attempt);
        Assert.AreEqual(retryAt, waiting.NextAttemptAt);
        Assert.AreEqual(MirrorPulseUploadOperationState.Succeeded, succeeded.State);
        Assert.IsNull(succeeded.NextAttemptAt);
    }

    [TestMethod]
    public void StateMachineRejectsTransitionsFromTerminalOperations()
    {
        var succeeded = MirrorPulseUploadOperationStateMachine.Succeed(
            MirrorPulseUploadOperationStateMachine.Start(Create()));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            MirrorPulseUploadOperationStateMachine.Start(succeeded));
    }

    private static MirrorPulseQueuedUpload Create() => new(
        Guid.NewGuid(),
        InstanceId.New(),
        MirrorPulseUploadOperationKind.Update,
        "file.txt",
        null,
        new byte[] { 4 },
        DateTimeOffset.UtcNow);
}

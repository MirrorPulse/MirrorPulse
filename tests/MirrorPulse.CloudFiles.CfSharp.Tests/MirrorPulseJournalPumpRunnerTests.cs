using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
public sealed class MirrorPulseJournalPumpRunnerTests
{
    [TestMethod]
    [DataRow("routing")]
    [DataRow("worker")]
    [DataRow("catalog")]
    [DataRow("acknowledgement")]
    public async Task FailedCommandDoesNotStopNextCommandOrDisappearFromHealth(string boundary)
    {
        var runner = new MirrorPulseJournalPumpRunner();
        MirrorPulseWorkerChangeCommand failed = Command();
        MirrorPulseWorkerChangeCommand valid = Command();
        var acknowledged = new HashSet<Guid>();
        string? code = null;
        bool inject = true;
        ValueTask<bool> Dispatch(MirrorPulseWorkerChangeCommand command, CancellationToken _)
        {
            if (inject && command.OperationId == failed.OperationId)
                throw boundary == "acknowledgement"
                    ? new MirrorPulseJournalAcknowledgementException("fixture ack rejected")
                    : new IOException("fixture boundary unavailable");
            acknowledged.Add(command.OperationId);
            return ValueTask.FromResult(true);
        }
        ValueTask Report(MirrorPulseWorkerChangeCommand? command, string error, Exception _, CancellationToken __)
        {
            Assert.AreEqual(failed.OperationId, command!.OperationId);
            code = error;
            if (boundary == "catalog") throw new IOException("fixture fault reporting unavailable");
            return ValueTask.CompletedTask;
        }
        ValueTask<MirrorPulseJournalUploadBatch> Read(CancellationToken _) => ValueTask.FromResult(
            new MirrorPulseJournalUploadBatch([failed, valid], 0, false));
        Assert.IsTrue(await runner.RunCycleAsync(Read, Dispatch, Report, CancellationToken.None));
        Assert.Contains(valid.OperationId, acknowledged);
        Assert.DoesNotContain(failed.OperationId, acknowledged);
        Assert.IsFalse(runner.Health.Healthy);
        Assert.AreEqual(1, runner.Health.PendingFaults);
        Assert.AreEqual(boundary == "acknowledgement" ? "JournalAcknowledgementFailed" : "JournalCommandFailed", code);
        inject = false;
        Assert.IsTrue(await runner.RunCycleAsync(Read, Dispatch, Report, CancellationToken.None));
        Assert.Contains(failed.OperationId, acknowledged);
        Assert.IsTrue(runner.Health.Healthy);
    }

    [TestMethod]
    public async Task ReadFailureIsVisibleAndNextCycleRecoversEvenWhenReportingFails()
    {
        var runner = new MirrorPulseJournalPumpRunner();
        await runner.RunCycleAsync(_ => throw new IOException("fixture source unavailable"),
            (_, _) => ValueTask.FromResult(true), (_, _, _, _) => throw new IOException("fixture catalog unavailable"), CancellationToken.None);
        Assert.AreEqual("JournalReadFailed", runner.Health.LastErrorCode);
        Assert.IsFalse(runner.Health.Healthy);
        await runner.RunCycleAsync(_ => ValueTask.FromResult(new MirrorPulseJournalUploadBatch([], 0, false)),
            (_, _) => ValueTask.FromResult(true), (_, _, _, _) => ValueTask.CompletedTask, CancellationToken.None);
        Assert.IsTrue(runner.Health.Healthy);
    }

    private static MirrorPulseWorkerChangeCommand Command() => new(Guid.NewGuid(), 1, InstanceId.New(), "root",
        MirrorPulseWorkerChangeKind.Create, "file.txt", null, null, false, null, DateTimeOffset.UtcNow);
}

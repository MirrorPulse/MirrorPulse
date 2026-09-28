using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseCrashRecoveryHarnessTests
{
    [TestMethod]
    public async Task HarnessRunsCloseReopenAndProbeInOrder()
    {
        var runtime = new RecordingRuntime();
        var harness = new MirrorPulseCrashRecoveryHarness(runtime);

        var report = await harness.RunAsync();

        CollectionAssert.AreEqual(RecordingRuntime.ExpectedEvents, runtime.Events);
        Assert.IsTrue(report.Closed);
        Assert.IsTrue(report.Reopened);
        Assert.IsTrue(report.StateReadable);
        Assert.IsTrue(report.LocalChangeFeedReady);
    }

    private sealed class RecordingRuntime : IMirrorPulseCrashRecoveryRuntime
    {
        public static readonly string[] ExpectedEvents = ["close", "reopen", "probe"];
        public List<string> Events { get; } = [];

        public ValueTask CloseAsync(CancellationToken cancellationToken)
        {
            Events.Add("close");
            return ValueTask.CompletedTask;
        }

        public ValueTask ReopenAsync(CancellationToken cancellationToken)
        {
            Events.Add("reopen");
            return ValueTask.CompletedTask;
        }

        public ValueTask<MirrorPulseRecoveryProbe> ProbeAsync(CancellationToken cancellationToken)
        {
            Events.Add("probe");
            return ValueTask.FromResult(new MirrorPulseRecoveryProbe(true, true));
        }
    }
}

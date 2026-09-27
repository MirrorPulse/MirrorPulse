using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerTerminationTests
{
    private static readonly string[] LongRunningCommand = ["/c", "ping 127.0.0.1 -n 10 > nul"];
    private static readonly string[] ExitCommand = ["/c", "exit 0"];

    [TestMethod]
    public async Task TerminatorForcesLongRunningWorkerAfterGracePeriod()
    {
        using var worker = StartCommand(LongRunningCommand);
        using var job = WorkerJobObject.Create();
        job.Attach(worker.Process);

        var result = await WorkerProcessTerminator.StopAsync(
            worker,
            job,
            new WorkerTerminationPolicy(TimeSpan.FromMilliseconds(50), 42));

        Assert.AreEqual(WorkerTerminationOutcome.Forced, result.Outcome);
        Assert.AreEqual(worker.ProcessId, result.ProcessId);
        Assert.IsTrue(worker.Process.HasExited);
    }

    [TestMethod]
    public async Task TerminatorReportsAlreadyExitedWorker()
    {
        using var worker = StartCommand(ExitCommand);
        await worker.WaitForExitAsync();
        using var job = WorkerJobObject.Create();

        var result = await WorkerProcessTerminator.StopAsync(
            worker,
            job,
            new WorkerTerminationPolicy(TimeSpan.FromSeconds(1)));

        Assert.AreEqual(WorkerTerminationOutcome.AlreadyExited, result.Outcome);
        Assert.AreEqual(0, result.ExitCode);
    }

    [TestMethod]
    public void PolicyRejectsNonPositiveGracePeriod()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkerTerminationPolicy(TimeSpan.Zero));
    }

    private static WorkerProcessHandle StartCommand(IReadOnlyList<string> arguments) => WorkerProcessLauncher.Start(new WorkerLaunchRequest(
        InstanceId.New(),
        WorkerSessionId.New(),
        Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
        AppContext.BaseDirectory,
        arguments));
}

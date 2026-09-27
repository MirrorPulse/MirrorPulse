using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerJobObjectTests
{
    private static readonly string[] LongRunningCommand = ["/c", "ping 127.0.0.1 -n 10 > nul"];

    [TestMethod]
    public async Task JobObjectCanOwnAndTerminateWorkerTree()
    {
        var commandShell = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var request = new WorkerLaunchRequest(
            MirrorPulse.Core.Contracts.InstanceId.New(),
            MirrorPulse.Core.Contracts.WorkerSessionId.New(),
            commandShell,
            AppContext.BaseDirectory,
            LongRunningCommand);
        using var worker = WorkerProcessLauncher.Start(request);
        using var job = WorkerJobObject.Create();

        job.Attach(worker.Process);
        job.Terminate(42);
        await worker.WaitForExitAsync();

        Assert.IsTrue(worker.Process.HasExited);
    }

    [TestMethod]
    public void JobObjectRejectsExitedProcess()
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = Environment.ProcessPath!,
                Arguments = "--version",
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.Start();
        process.WaitForExit();
        using var job = WorkerJobObject.Create();

        Assert.ThrowsExactly<InvalidOperationException>(() => job.Attach(process));
    }
}

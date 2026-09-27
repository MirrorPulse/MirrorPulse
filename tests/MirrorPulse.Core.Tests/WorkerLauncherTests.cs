using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerLauncherTests
{
    private static readonly string[] VersionArguments = ["--version"];

    [TestMethod]
    public async Task LauncherStartsProcessWithInstanceMetadata()
    {
        var request = new WorkerLaunchRequest(
            InstanceId.New(),
            WorkerSessionId.New(),
            Environment.ProcessPath!,
            AppContext.BaseDirectory,
            VersionArguments,
            new Dictionary<string, string> { ["MIRRORPULSE_TEST"] = "enabled" });
        using var worker = WorkerProcessLauncher.Start(request);

        await worker.WaitForExitAsync();

        Assert.AreEqual(request.InstanceId, worker.InstanceId);
        Assert.AreEqual(request.WorkerSessionId, worker.WorkerSessionId);
        Assert.IsTrue(worker.Process.HasExited);
        Assert.AreEqual(0, worker.Process.ExitCode);
    }

    [TestMethod]
    public void LaunchRequestCopiesArgumentsAndEnvironment()
    {
        var arguments = new[] { "--pipe", "mirrorpulse" };
        var environment = new Dictionary<string, string> { ["MP_MODE"] = "worker" };
        var request = new WorkerLaunchRequest(
            InstanceId.New(),
            WorkerSessionId.New(),
            Environment.ProcessPath!,
            AppContext.BaseDirectory,
            arguments,
            environment);

        arguments[0] = "changed";
        environment["MP_MODE"] = "changed";

        Assert.AreEqual("--pipe", request.Arguments[0]);
        Assert.AreEqual("worker", request.Environment["MP_MODE"]);
    }
}

using System.Text.Json;
using MirrorPulse.Cli;
using MirrorPulse.Control.Client;
using MirrorPulse.Control.Contracts;
using MirrorPulse.Control.Dispatch;
using MirrorPulse.Control.Transport;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Cli.Tests;

[TestClass]
public sealed class MirrorPulseCliConflictOutcomeTests
{
    [TestMethod]
    [DataRow("pending", 11)]
    [DataRow("resolved", 0)]
    [DataRow("failed", 7)]
    public async Task CliReportsActualHostCommandStateThroughCurrentUserPipe(string state, int expectedExit)
    {
        Guid conflictId = Guid.NewGuid();
        Guid commandId = Guid.NewGuid();
        string pipe = "MirrorPulse-conflict-cli-test-" + Guid.NewGuid().ToString("N");
        var dispatcher = new MirrorPulseControlDispatcher();
        dispatcher.Register<ConflictResolveArguments, MirrorPulseAppStatusResponse>(MirrorPulseControlCommands.ConflictResolve,
            (arguments, _) =>
            {
                Assert.AreEqual(conflictId, arguments.ConflictId);
                return ValueTask.FromResult(new MirrorPulseAppStatusResponse(1, 1, [], [],
                    ConflictCommand: new MirrorPulseConflictCommandResult(commandId, conflictId, state)));
            });
        using var shutdown = new CancellationTokenSource();
        var server = new MirrorPulseControlPipeServer(dispatcher.DispatchAsync, pipe);
        Task serve = server.ServeAsync(shutdown.Token);
        var client = new MirrorPulseControlClient(new MirrorPulseControlClientOptions { PipeName = pipe });
        await using var operations = new MirrorPulseCliHostOperations(new MirrorPulseHostStartupOptions(), client);
        try
        {
            foreach (bool json in new[] { false, true })
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                string[] arguments = json
                    ? ["--no-start", "--json", "conflict", "resolve", "--conflict-id", conflictId.ToString("D"), "--action", "Retry"]
                    : ["--no-start", "conflict", "resolve", "--conflict-id", conflictId.ToString("D"), "--action", "Retry"];
                int exit = await MirrorPulseCliApplication.RunAsync(arguments, output, error, hostOperations: operations, isPlatformSupported: () => true);
                Assert.AreEqual(expectedExit, exit);
                Assert.AreEqual(string.Empty, error.ToString());
                if (state != "resolved") Assert.IsFalse(output.ToString().Contains("resolved", StringComparison.OrdinalIgnoreCase));
                if (json)
                {
                    using JsonDocument result = JsonDocument.Parse(output.ToString());
                    JsonElement outcome = result.RootElement.GetProperty("data").GetProperty("conflictCommand");
                    Assert.AreEqual(state, outcome.GetProperty("state").GetString());
                    Assert.AreEqual(commandId, outcome.GetProperty("commandId").GetGuid());
                }
                else if (state != "resolved") StringAssert.Contains(output.ToString(), commandId.ToString("D"));
            }
        }
        finally { shutdown.Cancel(); await serve; }
    }
}

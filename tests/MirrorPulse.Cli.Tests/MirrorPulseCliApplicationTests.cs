using MirrorPulse.Cli;

namespace MirrorPulse.Cli.Tests;

[TestClass]
public sealed class MirrorPulseCliApplicationTests
{
    [TestMethod]
    public async Task VersionCommandPrintsVersionAndSucceeds()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["--version"], output, error);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(output.ToString(), "MirrorPulse mp ");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task HelpPrintsCommandTreeWithoutError()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(["--help"], output, error);

        Assert.AreEqual(0, exitCode);
        StringAssert.Contains(output.ToString(), "host status|start|stop|restart");
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task UnknownCommandUsesUsageExitCodeAndStderr()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(["unknown"], output, error);

        Assert.AreEqual(1, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        StringAssert.Contains(error.ToString(), "Unknown command");
    }

    [TestMethod]
    public async Task RecognizedCommandDoesNotStartHostBeforeItsHandlerExists()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(["host", "status"], output, error);

        Assert.AreEqual(8, exitCode);
        StringAssert.Contains(error.ToString(), "recognized");
    }
}

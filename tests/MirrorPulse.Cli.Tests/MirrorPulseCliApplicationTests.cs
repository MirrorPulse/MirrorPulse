using MirrorPulse.Cli;
using System.Text.Json;

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

    [TestMethod]
    public async Task JsonVersionUsesVersionedSchema()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["--json", "--version"], output, error);

        Assert.AreEqual(0, exitCode);
        using JsonDocument document = JsonDocument.Parse(output.ToString());
        Assert.AreEqual("1", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.AreEqual("version", document.RootElement.GetProperty("kind").GetString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    [TestMethod]
    public async Task JsonErrorsStayOnStderr()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["--json", "host", "status"], output, error);

        Assert.AreEqual(8, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        using JsonDocument document = JsonDocument.Parse(error.ToString());
        Assert.AreEqual("error", document.RootElement.GetProperty("kind").GetString());
        Assert.AreEqual("mp.control.unsupported", document.RootElement.GetProperty("code").GetString());
    }
}

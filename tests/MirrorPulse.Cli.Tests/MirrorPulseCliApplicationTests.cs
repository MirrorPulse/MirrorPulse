using System.Text.Json;
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

        var operations = new FakeHostOperations();
        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["host", "status"], output, error, hostOperations: operations);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, operations.EnsureStartedCount);
        StringAssert.Contains(output.ToString(), "fake-host");
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
        var operations = new FakeHostOperations();

        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["--json", "status"], output, error, hostOperations: operations);

        Assert.AreEqual(8, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
        using JsonDocument document = JsonDocument.Parse(error.ToString());
        Assert.AreEqual("error", document.RootElement.GetProperty("kind").GetString());
        Assert.AreEqual("mp.control.unsupported", document.RootElement.GetProperty("code").GetString());
    }

    [TestMethod]
    public async Task CredentialValuesAreAcceptedWithoutAppearingInSafeText()
    {
        using var credentials = await MirrorPulseCliCredentialInput.ResolveAsync(
            ["--username", "alice", "--password", "secret-value", "--credential-ref", "ref-1"]);

        Assert.AreEqual("alice", credentials.Username);
        Assert.AreEqual("ref-1", credentials.CredentialReference);
        Assert.AreEqual("secret-value", credentials.Password!.Reveal());
        Assert.IsFalse(credentials.ToString().Contains("secret-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task StandardInputPasswordIsReadAsASeparateSource()
    {
        using var credentials = await MirrorPulseCliCredentialInput.ResolveAsync(
            ["--password-stdin"], new StringReader("stdin-secret\n"));

        Assert.AreEqual(MirrorPulseCliCredentialSource.StandardInput, credentials.PasswordSource);
        Assert.AreEqual("stdin-secret", credentials.Password!.Reveal());
    }

    [TestMethod]
    public async Task DuplicatePasswordSourcesAreRejectedWithoutEchoingValues()
    {
        var exception = await Assert.ThrowsExactlyAsync<MirrorPulseCliCredentialException>(
            () => MirrorPulseCliCredentialInput.ResolveAsync(
                ["--password", "secret-value", "--password-prompt"],
                hiddenPrompt: _ => Task.FromResult(new MirrorPulseCliSecret("prompt-value"))));

        StringAssert.Contains(exception.Message, "one password source");
        Assert.IsFalse(exception.Message.Contains("secret-value", StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("prompt-value", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task OrdinaryCommandStartsHostUnlessNoStartIsSet()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var operations = new FakeHostOperations();

        int exitCode = await MirrorPulseCliApplication.RunAsync(
            ["status"], output, error, hostOperations: operations);

        Assert.AreEqual(8, exitCode);
        Assert.AreEqual(1, operations.EnsureStartedCount);

        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        operations.EnsureStartedCount = 0;
        exitCode = await MirrorPulseCliApplication.RunAsync(
            ["--no-start", "status"], output, error, hostOperations: operations);

        Assert.AreEqual(8, exitCode);
        Assert.AreEqual(0, operations.EnsureStartedCount);
    }

    private sealed class FakeHostOperations : IMirrorPulseCliHostOperations
    {
        public int EnsureStartedCount { get; set; }

        public Task EnsureStartedAsync(CancellationToken cancellationToken)
        {
            EnsureStartedCount++;
            return Task.CompletedTask;
        }

        public async Task<int> RunHostCommandAsync(
            string action,
            bool json,
            TextWriter output,
            TextWriter errorWriter,
            CancellationToken cancellationToken)
        {
            await output.WriteLineAsync("fake-host");
            return 0;
        }
    }
}

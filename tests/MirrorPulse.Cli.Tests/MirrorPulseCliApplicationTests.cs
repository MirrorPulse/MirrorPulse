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
}

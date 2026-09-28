namespace MirrorPulse.Core.Tests;

/// <summary>
/// Shared gate for Windows Cloud Files integration scenarios.
/// Native scenarios can be added without changing unit-test setup or cleanup.
/// </summary>
public sealed class CloudFilesIntegrationTestHarness
{
    public static bool IsSupported => OperatingSystem.IsWindows() && Environment.Is64BitProcess;

    public static async ValueTask RunAsync(
        Func<CancellationToken, ValueTask> scenario,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        if (!IsSupported)
        {
            throw new PlatformNotSupportedException("Cloud Files integration scenarios require a 64-bit Windows process.");
        }

        await scenario(cancellationToken).ConfigureAwait(false);
    }
}

[TestClass]
public sealed class CloudFilesIntegrationTestHarnessTests
{
    [TestMethod]
    public void HarnessRecognizesTheSupportedWindowsRunner()
    {
        Assert.IsTrue(CloudFilesIntegrationTestHarness.IsSupported);
    }

    [TestMethod]
    public async Task HarnessRunsAnIntegrationScenario()
    {
        var called = false;

        await CloudFilesIntegrationTestHarness.RunAsync(_ =>
        {
            called = true;
            return ValueTask.CompletedTask;
        });

        Assert.IsTrue(called);
    }
}

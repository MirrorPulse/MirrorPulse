using System.Runtime.InteropServices;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CloudFilesPlatformProbeTests
{
    [TestMethod]
    public void ProbeAcceptsWindowsX64WhenCfSharpReportsThePlatform()
    {
        var probe = new CloudFilesPlatformProbe(
            true,
            Architecture.X64,
            "win-x64",
            new FakePlatformReader(new CloudFilesPlatformInfo(10, 0, 0x500)));

        var result = probe.Probe();

        Assert.IsTrue(result.IsSupported);
        Assert.AreEqual("win-x64", result.RuntimeIdentifier);
        Assert.AreEqual((uint)0x500, result.Platform!.Value.IntegrationNumber);
    }

    [TestMethod]
    public void ProbeRejectsUnsupportedArchitectureBeforeCallingCfSharp()
    {
        var reader = new FakePlatformReader(new CloudFilesPlatformInfo(10, 0, 0x500));
        var probe = new CloudFilesPlatformProbe(true, Architecture.X86, "win-x86", reader);

        var result = probe.Probe();

        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual(0, reader.ReadCount);
        StringAssert.Contains(result.FailureReason!, "architecture");
    }

    private sealed class FakePlatformReader(CloudFilesPlatformInfo platform) : ICloudFilesPlatformReader
    {
        public int ReadCount { get; private set; }

        public CloudFilesPlatformInfo Read()
        {
            ReadCount++;
            return platform;
        }
    }
}

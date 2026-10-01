using System.Runtime.InteropServices;
using Microsoft.Win32;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulsePlatformTests
{
    [TestMethod]
    public void CurrentPlatformMatchesActualBuildArchitectureAndSystemProductType()
    {
        string? productType = Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\ProductOptions", "ProductType", null) as string;
        Assert.IsNotNull(productType);
        byte expectedType = productType switch { "WinNT" => 1, "LanmanNT" => 2, "ServerNT" => 3, _ => 0 };
        bool expected = MirrorPulsePlatform.IsSupported(true, Environment.OSVersion.Version,
            expectedType, RuntimeInformation.ProcessArchitecture);
        Assert.AreEqual(expected, MirrorPulsePlatform.IsCurrentSupported());
    }

    [TestMethod]
    [DataRow(true, "10.0.26100.0", 1, Architecture.X64, true)]
    [DataRow(true, "10.0.26100.0", 1, Architecture.Arm64, true)]
    [DataRow(true, "10.0.26100", 1, Architecture.Arm64, true)]
    [DataRow(true, "10.0.26200.0", 1, Architecture.Arm64, true)]
    [DataRow(true, "10.0.26099.9999", 1, Architecture.X64, false)]
    [DataRow(true, "10.0.22631.0", 1, Architecture.X64, false)]
    [DataRow(true, "10.0.19045.0", 1, Architecture.X64, false)]
    [DataRow(true, "10.0.26100.0", 2, Architecture.X64, false)]
    [DataRow(true, "10.0.26100.0", 3, Architecture.X64, false)]
    [DataRow(true, "10.0.26100.0", 0, Architecture.X64, false)]
    [DataRow(true, "10.0.26100.0", 1, Architecture.X86, false)]
    [DataRow(true, "10.0.26100.0", 1, Architecture.Arm, false)]
    [DataRow(false, "10.0.26100.0", 1, Architecture.X64, false)]
    public void SupportPolicyRequiresDesktopBuildAndSupportedArchitecture(
        bool windows, string version, int productType, Architecture architecture, bool supported)
    {
        Assert.AreEqual(supported, MirrorPulsePlatform.IsSupported(windows,
            new Version(version), (byte)productType, architecture));
    }
}

using System.Runtime.InteropServices;

namespace MirrorPulse.Core.Configuration;

/// <summary>Product support policy, independent of Cloud Files and persistence.</summary>
public static class MirrorPulsePlatform
{
    public const string MinimumVersion = "10.0.26100.0";
    public const string Requirement =
        "MirrorPulse requires a Microsoft-supported Windows 11 desktop release, version 24H2 (build 26100) or later, on x64 or ARM64. Windows 10 and Windows Server are not supported.";

    public static bool IsSupported(bool isWindows, Version version, byte productType, Architecture architecture)
    {
        ArgumentNullException.ThrowIfNull(version);
        var minimum = new Version(MinimumVersion);
        return isWindows && version >= new Version(minimum.Major, minimum.Minor, minimum.Build) && productType == 1 &&
            architecture is Architecture.X64 or Architecture.Arm64;
    }

    public static bool IsCurrentSupported()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var info = new OsVersionInfo { Size = (uint)Marshal.SizeOf<OsVersionInfo>(), ServicePack = string.Empty };
        // ProductType identifies workstation (1), domain controller (2), or server (3).
        // Environment.OSVersion supplies the actual build on .NET 10, regardless of app manifest.
        return GetVersionEx(ref info) && IsSupported(true, Environment.OSVersion.Version,
            info.ProductType, RuntimeInformation.ProcessArchitecture);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OsVersionInfo
    {
        public uint Size;
        public uint Major;
        public uint Minor;
        public uint Build;
        public uint PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string ServicePack;
        public ushort ServicePackMajor;
        public ushort ServicePackMinor;
        public ushort SuiteMask;
        public byte ProductType;
        public byte Reserved;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetVersionExW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVersionEx(ref OsVersionInfo versionInfo);
}

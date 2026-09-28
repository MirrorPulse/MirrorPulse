using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Immutable result of the Windows Cloud Files platform and process architecture check.
/// </summary>
public sealed record CloudFilesPlatformSnapshot(
    bool IsWindows,
    Architecture ProcessArchitecture,
    string RuntimeIdentifier,
    CloudFilesPlatformInfo? Platform,
    string? FailureReason)
{
    public bool IsSupported =>
        IsWindows
        && (ProcessArchitecture is Architecture.X64 or Architecture.Arm64)
        && Platform is not null;
}

/// <summary>
/// Reads the native Cloud Files platform snapshot through CfSharp.
/// </summary>
public interface ICloudFilesPlatformReader
{
    CloudFilesPlatformInfo Read();
}

[SupportedOSPlatform("windows10.0.16299")]
public sealed class CfSharpCloudFilesPlatformReader : ICloudFilesPlatformReader
{
    public CloudFilesPlatformInfo Read() => CloudFilesPlatform.GetCurrent();
}

/// <summary>
/// Validates the supported Windows and process architecture boundary before native registration.
/// </summary>
public sealed class CloudFilesPlatformProbe
{
    private readonly Func<bool> _isWindows;
    private readonly Architecture _processArchitecture;
    private readonly string _runtimeIdentifier;
    private readonly ICloudFilesPlatformReader _reader;

    [SupportedOSPlatform("windows10.0.16299")]
    public CloudFilesPlatformProbe()
        : this(
            OperatingSystem.IsWindows(),
            RuntimeInformation.ProcessArchitecture,
            RuntimeInformation.RuntimeIdentifier,
            new CfSharpCloudFilesPlatformReader())
    {
    }

    public CloudFilesPlatformProbe(
        bool isWindows,
        Architecture processArchitecture,
        string runtimeIdentifier,
        ICloudFilesPlatformReader reader)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        ArgumentNullException.ThrowIfNull(reader);
        _isWindows = () => isWindows;
        _processArchitecture = processArchitecture;
        _runtimeIdentifier = runtimeIdentifier.Trim();
        _reader = reader;
    }

    public CloudFilesPlatformSnapshot Probe()
    {
        if (!_isWindows())
        {
            return new(false, _processArchitecture, _runtimeIdentifier, null, "Windows is required.");
        }

        if (_processArchitecture is not (Architecture.X64 or Architecture.Arm64))
        {
            return new(true, _processArchitecture, _runtimeIdentifier, null, "The process architecture is not supported.");
        }

        try
        {
            var platform = _reader.Read();
            return new(true, _processArchitecture, _runtimeIdentifier, platform, null);
        }
        catch (Exception exception) when (exception is PlatformNotSupportedException or CloudFilesException)
        {
            return new(true, _processArchitecture, _runtimeIdentifier, null, exception.Message);
        }
    }
}

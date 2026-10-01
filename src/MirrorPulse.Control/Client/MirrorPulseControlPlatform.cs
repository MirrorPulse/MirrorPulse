using MirrorPulse.Core.Configuration;

namespace MirrorPulse.Control.Client;

/// <summary>Client-visible product platform checks without accessing Host state.</summary>
public static class MirrorPulseControlPlatform
{
    public static string Requirement => MirrorPulsePlatform.Requirement;
    public static bool IsCurrentSupported() => MirrorPulsePlatform.IsCurrentSupported();
}

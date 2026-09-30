namespace MirrorPulse.Control.Contracts;

/// <summary>
/// Version metadata for the current-user MirrorPulse control protocol.
/// </summary>
public static class MirrorPulseControlSchema
{
    public const int CurrentVersion = 1;

    public const string MediaType = "application/vnd.mirrorpulse.control+json";

    public const int MaximumFrameBytes = 4 * 1024 * 1024;
}

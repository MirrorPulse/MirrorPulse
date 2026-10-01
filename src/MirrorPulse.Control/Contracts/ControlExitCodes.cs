namespace MirrorPulse.Control.Contracts;

/// <summary>
/// Stable process exit codes used by the MirrorPulse CLI.
/// </summary>
public static class MirrorPulseControlExitCodes
{
    public const int Success = 0;
    public const int Usage = 1;
    public const int Validation = 2;
    public const int Unavailable = 3;
    public const int Timeout = 4;
    public const int Authentication = 5;
    public const int Authorization = 6;
    public const int Conflict = 7;
    public const int Unsupported = 8;
    public const int Storage = 9;
    public const int Cancelled = 10;
    public const int Pending = 11;
    public const int Internal = 70;
}

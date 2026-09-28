namespace MirrorPulse.Core.Sync;

public enum MirrorPulseVersionComparison
{
    Unknown,
    Same,
    LocalAdvanced,
    RemoteAdvanced,
    Diverged,
}

/// <summary>
/// Compares opaque local and remote revisions using only acknowledged equality and provider ancestry.
/// </summary>
public static class MirrorPulseVersionComparator
{
    public static MirrorPulseVersionComparison Compare(
        string? localRevision,
        string? acknowledgedRemoteRevision,
        string? incomingRemoteRevision,
        string? incomingPreviousRemoteRevision)
    {
        localRevision = Normalize(localRevision);
        acknowledgedRemoteRevision = Normalize(acknowledgedRemoteRevision);
        incomingRemoteRevision = Normalize(incomingRemoteRevision);
        incomingPreviousRemoteRevision = Normalize(incomingPreviousRemoteRevision);

        if (incomingRemoteRevision is null)
        {
            return MirrorPulseVersionComparison.Unknown;
        }

        if (localRevision is not null && incomingRemoteRevision == localRevision)
        {
            return MirrorPulseVersionComparison.Same;
        }

        if (acknowledgedRemoteRevision is not null
            && incomingRemoteRevision == acknowledgedRemoteRevision
            && localRevision != acknowledgedRemoteRevision)
        {
            return MirrorPulseVersionComparison.LocalAdvanced;
        }

        if (acknowledgedRemoteRevision is not null
            && incomingPreviousRemoteRevision == acknowledgedRemoteRevision)
        {
            return localRevision == acknowledgedRemoteRevision
                ? MirrorPulseVersionComparison.RemoteAdvanced
                : MirrorPulseVersionComparison.Diverged;
        }

        return MirrorPulseVersionComparison.Unknown;
    }

    private static string? Normalize(string? revision) =>
        string.IsNullOrWhiteSpace(revision) ? null : revision;
}

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Operations an Adapter declares before MP creates an instance.
/// </summary>
public sealed record AdapterCapabilities
{
    public AdapterCapabilities(
        bool network,
        bool sourceDirectory,
        bool remoteChanges,
        bool rangeRead)
    {
        Network = network;
        SourceDirectory = sourceDirectory;
        RemoteChanges = remoteChanges;
        RangeRead = rangeRead;
    }

    public bool Network { get; }

    public bool SourceDirectory { get; }

    public bool RemoteChanges { get; }

    public bool RangeRead { get; }
}

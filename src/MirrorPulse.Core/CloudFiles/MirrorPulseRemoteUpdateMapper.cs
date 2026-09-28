using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteUpdateMapper
{
    public static MirrorPulseRemoteOperation Map(CloudRemoteChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind != CloudRemoteChangeKind.FileUpsert
            || change.PreviousRemoteRevision is null
            || change.PreviousRelativePath is not null)
        {
            throw new InvalidDataException("The remote change is not a file update.");
        }

        return MirrorPulseRemoteOperationFactory.Create(change, MirrorPulseRemoteOperationKind.UpdateFile);
    }
}

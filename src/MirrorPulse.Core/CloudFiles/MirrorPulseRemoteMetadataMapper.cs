using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteMetadataMapper
{
    public static MirrorPulseRemoteOperation Map(CloudRemoteChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind != CloudRemoteChangeKind.MetadataUpdate)
        {
            throw new InvalidDataException("The remote change is not a metadata update.");
        }

        return MirrorPulseRemoteOperationFactory.Create(change, MirrorPulseRemoteOperationKind.MetadataUpdate);
    }
}

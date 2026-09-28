using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteDeleteMapper
{
    public static MirrorPulseRemoteOperation Map(CloudRemoteChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind != CloudRemoteChangeKind.Delete)
        {
            throw new InvalidDataException("The remote change is not a deletion.");
        }

        return MirrorPulseRemoteOperationFactory.Create(change, MirrorPulseRemoteOperationKind.Delete);
    }
}

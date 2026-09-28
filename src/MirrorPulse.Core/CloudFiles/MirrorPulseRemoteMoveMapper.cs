using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteMoveMapper
{
    public static MirrorPulseRemoteOperation Map(CloudRemoteChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind != CloudRemoteChangeKind.Move
            || string.IsNullOrWhiteSpace(change.PreviousRelativePath))
        {
            throw new InvalidDataException("The remote change is not a move.");
        }

        return MirrorPulseRemoteOperationFactory.Create(change, MirrorPulseRemoteOperationKind.Move);
    }
}

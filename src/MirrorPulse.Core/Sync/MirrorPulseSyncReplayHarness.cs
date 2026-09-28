using System.Collections.ObjectModel;
using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Sync;

[SupportedOSPlatform("windows10.0.16299")]
public sealed record MirrorPulseSyncReplayResult(
    IReadOnlyList<Guid> LocalOperationIds,
    IReadOnlyList<string> RemoteChangeIds);

/// <summary>
/// Presents CfSharp journal order and remote operations without native or network access.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseSyncReplayHarness
{
    public static MirrorPulseSyncReplayResult Replay(
        IEnumerable<MirrorPulseWorkerChangeCommand> localOperations,
        IEnumerable<CloudRemoteChange> remoteOperations)
    {
        ArgumentNullException.ThrowIfNull(localOperations);
        ArgumentNullException.ThrowIfNull(remoteOperations);
        var local = MirrorPulseUploadDependencyPlanner.Plan(localOperations)
            .Select(operation => operation.OperationId)
            .ToArray();
        var remote = remoteOperations.ToArray();
        if (remote.Select(operation => operation.ChangeId).Distinct(StringComparer.Ordinal).Count() != remote.Length)
        {
            throw new InvalidDataException("Remote replay cannot contain duplicate change IDs.");
        }

        return new(
            new ReadOnlyCollection<Guid>(local),
            new ReadOnlyCollection<string>(remote.Select(operation => operation.ChangeId).ToArray()));
    }
}

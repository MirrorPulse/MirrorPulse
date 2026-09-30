using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseLocalBatchPlan(
    IReadOnlyList<MirrorPulseWorkerChangeCommand> Commands,
    IReadOnlyList<Guid> DirectoryMetadataOperationIds,
    bool RequiresFullRescan);

/// <summary>Projects CfSharp's durable local journal into per-Adapter commands without a second queue.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseLocalBatchMapper
{
    public static MirrorPulseLocalBatchPlan Map(
        CloudLocalChangeBatch batch,
        MirrorPulseRootRouter router)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(router);
        if (batch.RequiresFullRescan)
        {
            return new([], [], true);
        }

        var commands = new List<MirrorPulseWorkerChangeCommand>(batch.Changes.Count);
        var directoryMetadata = new List<Guid>();
        foreach (CloudLocalChange change in batch.Changes)
        {
            MirrorPulseRoutedItem current = router.ResolvePath(change.RelativePath);
            if (change.IsDirectory && change.Kind == CloudLocalChangeKind.MetadataUpdate)
            {
                // Directory timestamps and availability flags do not have a Worker mutation.
                // Retain the real child operations in the batch and acknowledge this local
                // bookkeeping event so it cannot remain a permanent pending upload.
                directoryMetadata.Add(change.OperationId);
                continue;
            }

            if (current.RelativePath.Length == 0)
            {
                throw new InvalidDataException(
                    $"First-level Adapter directory change '{change.Kind}: {change.RelativePath}' requires root reconciliation.");
            }

            MirrorPulseRoutedItem? previous = change.PreviousRelativePath is null
                ? null
                : router.ResolvePath(change.PreviousRelativePath);
            if (previous is not null && previous.InstanceId != current.InstanceId)
            {
                throw new InvalidDataException("A move between Adapter instances requires reconciliation.");
            }

            commands.Add(new MirrorPulseWorkerChangeCommand(
                change.OperationId,
                change.Sequence,
                current.InstanceId,
                current.RootKey,
                ToWorkerKind(change.Kind),
                current.RelativePath,
                previous?.RootKey,
                previous?.RelativePath,
                change.IsDirectory,
                change.ItemId,
                change.ObservedAt));
        }

        return new(commands.AsReadOnly(), directoryMetadata.AsReadOnly(), false);
    }

    private static MirrorPulseWorkerChangeKind ToWorkerKind(CloudLocalChangeKind kind) => kind switch
    {
        CloudLocalChangeKind.Create => MirrorPulseWorkerChangeKind.Create,
        CloudLocalChangeKind.ContentUpdate => MirrorPulseWorkerChangeKind.ContentUpdate,
        CloudLocalChangeKind.MetadataUpdate => MirrorPulseWorkerChangeKind.MetadataUpdate,
        CloudLocalChangeKind.Move => MirrorPulseWorkerChangeKind.Move,
        CloudLocalChangeKind.Delete => MirrorPulseWorkerChangeKind.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}

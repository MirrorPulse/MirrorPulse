using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Adapters.LocalDirectory;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CfSharp.CrashProbe;

public enum DurabilityBoundary { RemoteWrite, JournalAcknowledgement, SnapshotSave, CatalogSave, PendingRemoteBatch, ConflictCopy, RescanCheckpoint }
public enum FaultTiming { Before, After }
public enum FaultMode { Throw, Exit }

/// <summary>Test-only faults around actual persistence APIs, without Cloud Files registration.</summary>
[SupportedOSPlatform("windows10.0.19041")]
public static class DurabilityFaultProbe
{
    public const int ExitFaultCode = 73;
    public const int ExceptionFaultCode = 74;
    public static readonly Guid OperationId = new("77759854-de86-4b71-9293-1ee4f4f79154");
    public static readonly InstanceId Instance = InstanceId.Parse("a1f31ddc-4f42-462f-92ee-3ff6e6ab9c43");

    public static async Task<int> RunAsync(string[] arguments)
    {
        string root = Path.GetFullPath(arguments[0]);
        if (!File.Exists(Path.Combine(root, ".mp-fault-fixture")) ||
            !Enum.TryParse(arguments[1], out DurabilityBoundary boundary) || !Enum.IsDefined(boundary) ||
            !Enum.TryParse(arguments[2], out FaultTiming timing) || !Enum.IsDefined(timing) ||
            !Enum.TryParse(arguments[3], out FaultMode mode) || !Enum.IsDefined(mode))
            return 2;

        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.SyncRootPath);
        try
        {
            switch (boundary)
            {
                case DurabilityBoundary.RescanCheckpoint:
                    await using (MirrorPulseProductCatalog catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
                    {
                        MirrorPulseFullRescanCheckpoint checkpoint = await catalog.BeginFullRescanAsync();
                        await InjectAsync(async () => await catalog.UpdateFullRescanAsync(checkpoint.Generation, MirrorPulseFullRescanPhase.Running), timing, mode);
                    }
                    break;
                case DurabilityBoundary.ConflictCopy:
                    await File.WriteAllTextAsync(Path.Combine(paths.SyncRootPath, "note.txt"), "original side");
                    var copies = new MirrorPulseConflictCopyStore(paths, checkpoint =>
                    {
                        if (checkpoint == (timing == FaultTiming.Before ? MirrorPulseConflictCopyCheckpoint.StagingFlushed :
                            MirrorPulseConflictCopyCheckpoint.ManifestCommitted)) Fail(mode);
                    });
                    MirrorPulseConflictRecord conflict = CopyConflict();
                    await copies.PreserveAsync(conflict, MirrorPulseConflictPreservedSide.Local, token => copies.OpenLocalAsync(conflict, token));
                    break;
                case DurabilityBoundary.RemoteWrite:
                    var writer = new MirrorPulseLocalDirectoryWriter(Path.Combine(root, "remote"));
                    await writer.WriteAsync("document.txt", "before"u8.ToArray());
                    await InjectAsync(async () => { await writer.WriteAsync("document.txt", "after"u8.ToArray()); }, timing, mode);
                    break;
                case DurabilityBoundary.JournalAcknowledgement:
                    await using (ICloudStateStore store = await MirrorPulseCfSharpStateStoreFactory.Create(paths)
                        .OpenAsync(new CloudStateStoreContext(paths.SyncRootPath)))
                    {
                        await using (ICloudStateTransaction seed = await store.BeginTransactionAsync())
                        {
                            Guid itemId = Guid.NewGuid();
                            await seed.Items.UpsertAsync(new CloudItemState(
                                itemId, "document", "document.txt", CloudItemKind.File, null, null, false, DateTimeOffset.UtcNow));
                            await seed.Operations.EnqueueAsync(new CloudOperationJournalEntry(
                                OperationId, CloudStateOperationKind.ContentUpdate, itemId, [1], DateTimeOffset.UtcNow));
                            await seed.CommitAsync();
                        }

                        await InjectAsync(async () =>
                        {
                            await using ICloudStateTransaction acknowledgement = await store.BeginTransactionAsync();
                            await acknowledgement.Operations.RemoveAsync(OperationId);
                            await acknowledgement.CommitAsync();
                        }, timing, mode);
                    }
                    break;
                case DurabilityBoundary.SnapshotSave:
                    var snapshots = new MirrorPulseFileRemotePollSnapshotStore(paths.DataRootPath);
                    await snapshots.SaveAsync(Instance, Snapshot("before"));
                    await InjectAsync(() => snapshots.SaveAsync(Instance, Snapshot("after")), timing, mode);
                    break;
                case DurabilityBoundary.CatalogSave:
                    await using (MirrorPulseProductCatalog catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
                    {
                        await catalog.SaveUserCommandAsync(new MirrorPulseUserCommandRecord(OperationId, "fixture", "document", "before"));
                        await InjectAsync(async () =>
                        {
                            await catalog.SaveUserCommandAsync(new MirrorPulseUserCommandRecord(OperationId, "fixture", "document", "after"));
                        }, timing, mode);
                    }
                    break;
                case DurabilityBoundary.PendingRemoteBatch:
                    await using (MirrorPulseProductCatalog catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
                    {
                        var pending = new MirrorPulseCatalogRemotePollPendingStore(catalog);
                        await InjectAsync(() => pending.SaveAsync(Instance,
                            new(Instance + "/fixture", new byte[32], "Local", Snapshot("before"), Snapshot("after")),
                            CancellationToken.None), timing, mode);
                    }
                    break;
            }
        }
        catch (InjectedBoundaryException)
        {
            return ExceptionFaultCode;
        }

        return 3; // Every accepted scenario must inject the requested failure.
    }

    private static async ValueTask InjectAsync(Func<ValueTask> action, FaultTiming timing, FaultMode mode)
    {
        if (timing == FaultTiming.Before) Fail(mode);
        await action();
        Fail(mode);
    }

    private static void Fail(FaultMode mode)
    {
        if (mode == FaultMode.Exit) Environment.Exit(ExitFaultCode);
        throw new InjectedBoundaryException();
    }

    private static Dictionary<string, MirrorPulseRemoteSnapshotEntry> Snapshot(string revision) =>
        new Dictionary<string, MirrorPulseRemoteSnapshotEntry>
        {
            ["document"] = new("document", revision, CloudItemKind.File, "document.txt", 5,
                new MirrorPulseRemoteSnapshotMetadata(CloudItemKind.File, FileAttributes.Normal, null, null, null, null)),
        };

    public static MirrorPulseConflictRecord CopyConflict() => new(OperationId, Instance, "fixture", "note.txt",
        MirrorPulseConflictReason.Content, MirrorPulseVersionComparison.Diverged, "before", "after",
        new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));

    private sealed class InjectedBoundaryException : Exception;
}

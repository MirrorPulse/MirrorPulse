using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseEndToEndSyncSimulationTests
{
    private static readonly string[] ExpectedRemoteIds = ["remote-1"];

    [TestMethod]
    public async Task SimulationReplaysBothSidesAndNotifiesPendingConflicts()
    {
        var instanceId = InstanceId.New();
        var localId = Guid.NewGuid();
        var local = new MirrorPulseWorkerChangeCommand(
            localId, 1, instanceId, "docs", MirrorPulseWorkerChangeKind.Create,
            "a.txt", null, null, false, null, DateTimeOffset.UtcNow);
        var remote = CreateRemote("remote-1");
        var conflictId = Guid.NewGuid();
        var conflict = new MirrorPulseConflictRecord(
            conflictId,
            instanceId,
            "remote-1",
            "docs/a.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local-1",
            "remote-1",
            DateTimeOffset.UtcNow);

        var result = await MirrorPulseEndToEndSyncSimulation.RunAsync(
            Enumerable.Repeat(local, 1),
            Enumerable.Repeat(remote, 1),
            Enumerable.Repeat(conflict, 1));

        CollectionAssert.AreEqual(Enumerable.Repeat(localId, 1).ToArray(), result.Replay.LocalOperationIds.ToArray());
        CollectionAssert.AreEqual(ExpectedRemoteIds, result.Replay.RemoteChangeIds.ToArray());
        CollectionAssert.AreEqual(Enumerable.Repeat(conflictId, 1).ToArray(), result.PendingConflictIds.ToArray());
        Assert.AreEqual(1, result.NotificationCount);
    }

    [TestMethod]
    public async Task SimulationLeavesResolvedConflictsOutOfPendingResults()
    {
        var resolved = new MirrorPulseConflictRecord(
            Guid.NewGuid(),
            InstanceId.New(),
            "remote-1",
            "docs/a.txt",
            MirrorPulseConflictReason.Content,
            MirrorPulseVersionComparison.Diverged,
            "local-1",
            "remote-1",
            DateTimeOffset.UtcNow,
            MirrorPulseConflictStatus.Resolved);

        var result = await MirrorPulseEndToEndSyncSimulation.RunAsync([], [], [resolved]);

        Assert.IsEmpty(result.PendingConflictIds);
        Assert.AreEqual(0, result.NotificationCount);
    }

    private static CloudRemoteChange CreateRemote(string changeId) => new(
        changeId,
        CloudRemoteChangeKind.FileUpsert,
        "object-1",
        "revision-1",
        CloudItemKind.File,
        "docs\\a.txt",
        null,
        "revision-0",
        null,
        1,
        CloudPlaceholderMetadata.CreateFileBuilder().Build(),
        ReadOnlyMemory<byte>.Empty);
}

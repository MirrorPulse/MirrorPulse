using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseSyncReplayHarnessTests
{
    private static readonly string[] ExpectedRemoteIds = ["remote-1"];

    [TestMethod]
    public void HarnessReplaysLocalDependenciesBeforeRemoteChanges()
    {
        var firstId = Guid.NewGuid();
        var first = new MirrorPulseQueuedUpload(
            firstId,
            InstanceId.New(),
            MirrorPulseUploadOperationKind.Create,
            "docs/a.txt",
            null,
            new byte[] { 1 },
            DateTimeOffset.UtcNow);
        var second = new MirrorPulseQueuedUpload(
            Guid.NewGuid(),
            first.InstanceId,
            MirrorPulseUploadOperationKind.Update,
            "docs/a.txt",
            null,
            new byte[] { 2 },
            first.CreatedAt.AddMinutes(1),
            [firstId]);
        var remote = new MirrorPulseRemoteOperation(
            MirrorPulseRemoteOperationKind.MetadataUpdate,
            "remote-1",
            "object-1",
            "revision-1",
            "docs\\a.txt",
            CloudItemKind.File,
            null,
            "revision-0",
            null,
            null,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);

        var result = MirrorPulseSyncReplayHarness.Replay([second, first], [remote]);

        CollectionAssert.AreEqual(new[] { firstId, second.OperationId }, result.LocalOperationIds.ToArray());
        CollectionAssert.AreEqual(ExpectedRemoteIds, result.RemoteChangeIds.ToArray());
    }

    [TestMethod]
    public void HarnessRejectsDuplicateRemoteChangeIds()
    {
        var remote = CreateRemote("remote-1");

        Assert.ThrowsExactly<InvalidDataException>(() =>
            MirrorPulseSyncReplayHarness.Replay([], [remote, remote]));
    }

    private static MirrorPulseRemoteOperation CreateRemote(string changeId) => new(
        MirrorPulseRemoteOperationKind.Delete,
        changeId,
        "object-1",
        "revision-1",
        "docs\\a.txt",
        CloudItemKind.File,
        null,
        "revision-0",
        null,
        null,
        null,
        ReadOnlyMemory<byte>.Empty);
}

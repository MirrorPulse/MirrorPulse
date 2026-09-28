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
        var instance = InstanceId.New();
        var first = new MirrorPulseWorkerChangeCommand(
            firstId, 1, instance, "docs", MirrorPulseWorkerChangeKind.Create,
            "a.txt", null, null, false, null, DateTimeOffset.UtcNow);
        var second = first with
        {
            OperationId = Guid.NewGuid(),
            Sequence = 2,
            Kind = MirrorPulseWorkerChangeKind.ContentUpdate,
        };
        var remote = new CloudRemoteChange(
            "remote-1",
            CloudRemoteChangeKind.MetadataUpdate,
            "object-1",
            "revision-1",
            CloudItemKind.File,
            "docs\\a.txt",
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

    private static CloudRemoteChange CreateRemote(string changeId) => new(
        changeId,
        CloudRemoteChangeKind.Delete,
        "object-1",
        "revision-1",
        CloudItemKind.File,
        "docs\\a.txt",
        null,
        "revision-0",
        null,
        null,
        null,
        ReadOnlyMemory<byte>.Empty);
}

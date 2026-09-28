using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteCreateMapperTests
{
    [TestMethod]
    public void MapperBuildsCreateOperationFromFileUpsert()
    {
        var change = CreateChange();
        var operation = MirrorPulseRemoteCreateMapper.Map(change);

        Assert.AreEqual(MirrorPulseRemoteOperationKind.CreateFile, operation.Kind);
        Assert.AreEqual("remote-file", operation.RemoteId);
        Assert.AreEqual("docs\\new.txt", operation.RelativePath);
        Assert.AreEqual(12, operation.Length);
        CollectionAssert.AreEqual(new byte[] { 4 }, operation.CursorAfter.ToArray());
    }

    [TestMethod]
    public void MapperRejectsUpdatesAndOtherKinds()
    {
        var update = new CloudRemoteChange(
            "change-update",
            CloudRemoteChangeKind.FileUpsert,
            "remote-file",
            "revision-2",
            CloudItemKind.File,
            "docs/new.txt",
            null,
            "revision-1",
            null,
            12,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);
        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseRemoteCreateMapper.Map(update));
    }

    private static CloudRemoteChange CreateChange() => new(
        "change-create",
        CloudRemoteChangeKind.FileUpsert,
        "remote-file",
        "revision-1",
        CloudItemKind.File,
        "docs/new.txt",
        null,
        null,
        null,
        12,
        CloudPlaceholderMetadata.CreateFileBuilder().Build(),
        new byte[] { 4 });
}

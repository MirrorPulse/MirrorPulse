using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteDeleteMapperTests
{
    [TestMethod]
    public void MapperBuildsDeleteOperationWithoutPayload()
    {
        var change = new CloudRemoteChange(
            "change-delete",
            CloudRemoteChangeKind.Delete,
            "remote-file",
            "revision-4",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            "revision-3",
            null,
            null,
            null,
            new byte[] { 9 });

        var operation = MirrorPulseRemoteDeleteMapper.Map(change);

        Assert.AreEqual(MirrorPulseRemoteOperationKind.Delete, operation.Kind);
        Assert.AreEqual("docs\\report.txt", operation.RelativePath);
        Assert.IsNull(operation.Length);
        CollectionAssert.AreEqual(new byte[] { 9 }, operation.CursorAfter.ToArray());
    }

    [TestMethod]
    public void MapperRejectsNonDeleteChanges()
    {
        var change = new CloudRemoteChange(
            "change-file",
            CloudRemoteChangeKind.FileUpsert,
            "remote-file",
            "revision-4",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            "revision-3",
            null,
            1,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);

        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseRemoteDeleteMapper.Map(change));
    }
}

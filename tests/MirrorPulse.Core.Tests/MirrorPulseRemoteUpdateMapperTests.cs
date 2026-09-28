using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteUpdateMapperTests
{
    [TestMethod]
    public void MapperBuildsUpdateOperationFromRevisionedFileUpsert()
    {
        var change = new CloudRemoteChange(
            "change-update",
            CloudRemoteChangeKind.FileUpsert,
            "remote-file",
            "revision-2",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            "revision-1",
            null,
            42,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);

        var operation = MirrorPulseRemoteUpdateMapper.Map(change);

        Assert.AreEqual(MirrorPulseRemoteOperationKind.UpdateFile, operation.Kind);
        Assert.AreEqual("revision-1", operation.PreviousRemoteRevision);
        Assert.AreEqual("docs\\report.txt", operation.RelativePath);
    }

    [TestMethod]
    public void MapperRejectsCreateWithoutPreviousRevision()
    {
        var change = new CloudRemoteChange(
            "change-create",
            CloudRemoteChangeKind.FileUpsert,
            "remote-file",
            "revision-1",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            null,
            null,
            42,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);

        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseRemoteUpdateMapper.Map(change));
    }
}

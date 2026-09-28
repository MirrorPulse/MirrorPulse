using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteMetadataMapperTests
{
    [TestMethod]
    public void MapperBuildsMetadataOperationWithRemoteMetadata()
    {
        var metadata = CloudPlaceholderMetadata.CreateFileBuilder().Build();
        var change = new CloudRemoteChange(
            "change-metadata",
            CloudRemoteChangeKind.MetadataUpdate,
            "remote-file",
            "revision-5",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            "revision-4",
            null,
            null,
            metadata,
            ReadOnlyMemory<byte>.Empty);

        var operation = MirrorPulseRemoteMetadataMapper.Map(change);

        Assert.AreEqual(MirrorPulseRemoteOperationKind.MetadataUpdate, operation.Kind);
        Assert.AreSame(metadata, operation.Metadata);
        Assert.AreEqual("docs\\report.txt", operation.RelativePath);
    }

    [TestMethod]
    public void MapperRejectsFileUpserts()
    {
        var change = new CloudRemoteChange(
            "change-file",
            CloudRemoteChangeKind.FileUpsert,
            "remote-file",
            "revision-5",
            CloudItemKind.File,
            "docs\\report.txt",
            null,
            "revision-4",
            null,
            1,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(),
            ReadOnlyMemory<byte>.Empty);

        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseRemoteMetadataMapper.Map(change));
    }
}

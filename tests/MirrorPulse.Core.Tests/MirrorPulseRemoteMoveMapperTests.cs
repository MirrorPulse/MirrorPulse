using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteMoveMapperTests
{
    [TestMethod]
    public void MapperBuildsMoveOperationWithBothPaths()
    {
        var change = new CloudRemoteChange(
            "change-move",
            CloudRemoteChangeKind.Move,
            "remote-file",
            "revision-3",
            CloudItemKind.File,
            "archive\\report.txt",
            null,
            "revision-2",
            "docs\\report.txt",
            null,
            null,
            ReadOnlyMemory<byte>.Empty);

        var operation = MirrorPulseRemoteMoveMapper.Map(change);

        Assert.AreEqual(MirrorPulseRemoteOperationKind.Move, operation.Kind);
        Assert.AreEqual("docs\\report.txt", operation.PreviousRelativePath);
        Assert.AreEqual("archive\\report.txt", operation.RelativePath);
    }

    [TestMethod]
    public void MapperRejectsMoveWithoutSourcePath()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CloudRemoteChange(
            "change-invalid-move",
            CloudRemoteChangeKind.Move,
            "remote-file",
            "revision-3",
            CloudItemKind.File,
            "archive\\report.txt",
            null,
            "revision-2",
            null,
            null,
            null,
            ReadOnlyMemory<byte>.Empty));
    }
}

using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalMoveUploadTests
{
    [TestMethod]
    public void MoveOperationContainsSourceAndDestinationPaths()
    {
        var operation = MirrorPulseLocalMoveUpload.Create(
            Guid.NewGuid(),
            InstanceId.New(),
            "old/report.txt",
            "archive/report.txt",
            DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseUploadOperationKind.Move, operation.Kind);
        Assert.AreEqual("old/report.txt", operation.RelativePath);
        Assert.AreEqual("archive/report.txt", operation.DestinationPath);
        Assert.AreEqual(0, operation.Payload.Length);
    }
}

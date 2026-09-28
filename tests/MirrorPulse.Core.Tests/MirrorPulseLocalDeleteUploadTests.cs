using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalDeleteUploadTests
{
    [TestMethod]
    public void DeleteOperationContainsOnlyTheRemovedPath()
    {
        var operation = MirrorPulseLocalDeleteUpload.Create(
            Guid.NewGuid(),
            InstanceId.New(),
            "obsolete/report.txt",
            DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseUploadOperationKind.Delete, operation.Kind);
        Assert.AreEqual("obsolete/report.txt", operation.RelativePath);
        Assert.IsNull(operation.DestinationPath);
        Assert.AreEqual(0, operation.Payload.Length);
    }
}

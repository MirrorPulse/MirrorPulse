using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalUpdateUploadTests
{
    [TestMethod]
    public void UpdateOperationPreservesPathAndChangedBytes()
    {
        var operation = MirrorPulseLocalUpdateUpload.Create(
            Guid.NewGuid(),
            InstanceId.New(),
            "docs/report.txt",
            new byte[] { 3, 1, 4 },
            DateTimeOffset.UtcNow);

        Assert.AreEqual(MirrorPulseUploadOperationKind.Update, operation.Kind);
        Assert.AreEqual("docs/report.txt", operation.RelativePath);
        Assert.IsNull(operation.DestinationPath);
        CollectionAssert.AreEqual(new byte[] { 3, 1, 4 }, operation.Payload.ToArray());
    }
}

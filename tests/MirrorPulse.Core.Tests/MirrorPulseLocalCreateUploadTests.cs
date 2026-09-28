using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseLocalCreateUploadTests
{
    [TestMethod]
    public void CreateOperationContainsLocalFilePayload()
    {
        var operationId = Guid.NewGuid();
        var instanceId = InstanceId.New();
        var createdAt = DateTimeOffset.UtcNow;

        var operation = MirrorPulseLocalCreateUpload.Create(
            operationId,
            instanceId,
            "photos/new.jpg",
            new byte[] { 9, 8, 7 },
            createdAt);

        Assert.AreEqual(operationId, operation.OperationId);
        Assert.AreEqual(instanceId, operation.InstanceId);
        Assert.AreEqual(MirrorPulseUploadOperationKind.Create, operation.Kind);
        Assert.AreEqual("photos/new.jpg", operation.RelativePath);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7 }, operation.Payload.ToArray());
    }
}

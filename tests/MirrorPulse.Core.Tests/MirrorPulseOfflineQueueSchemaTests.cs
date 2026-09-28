using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseOfflineQueueSchemaTests
{
    [TestMethod]
    public void SchemaRetainsOperationIdentityPayloadAndDependencies()
    {
        var dependency = Guid.NewGuid();
        var operation = new MirrorPulseQueuedUpload(
            Guid.NewGuid(),
            InstanceId.New(),
            MirrorPulseUploadOperationKind.Update,
            "docs/report.txt",
            null,
            new byte[] { 1, 2 },
            DateTimeOffset.UtcNow,
            [dependency]);

        Assert.AreEqual(1, MirrorPulseQueuedUpload.SchemaVersion);
        Assert.AreEqual(MirrorPulseUploadOperationState.Pending, operation.State);
        Assert.AreEqual(dependency, operation.Dependencies.Single());
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, operation.Payload.ToArray());
    }

    [TestMethod]
    public void SchemaRejectsInvalidMoveAndTerminalRetryData()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new MirrorPulseQueuedUpload(
            Guid.NewGuid(),
            InstanceId.New(),
            MirrorPulseUploadOperationKind.Move,
            "old.txt",
            null,
            ReadOnlyMemory<byte>.Empty,
            DateTimeOffset.UtcNow));

        Assert.ThrowsExactly<ArgumentException>(() => new MirrorPulseQueuedUpload(
            Guid.NewGuid(),
            InstanceId.New(),
            MirrorPulseUploadOperationKind.Delete,
            "old.txt",
            null,
            ReadOnlyMemory<byte>.Empty,
            DateTimeOffset.UtcNow,
            state: MirrorPulseUploadOperationState.Succeeded,
            nextAttemptAt: DateTimeOffset.UtcNow));
    }
}

using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseUploadQueueTests
{
    [TestMethod]
    public async Task QueueDeduplicatesRetriesAndAcknowledgesUpdates()
    {
        var store = new InMemoryMirrorPulseUploadQueueStore();
        var queue = new MirrorPulseUploadDispatcher(store);
        var request = new MirrorPulseUploadRequest(
            Guid.NewGuid(),
            InstanceId.New(),
            "docs/report.txt",
            new byte[] { 1, 2, 3 },
            DateTimeOffset.UtcNow);

        await queue.EnqueueAsync(request);
        await queue.EnqueueAsync(request);
        var pending = await queue.TakeAsync(10);

        Assert.HasCount(1, pending);
        Assert.AreEqual(request.OperationId, pending[0].OperationId);
        await queue.AcknowledgeAsync(request.OperationId);
        Assert.HasCount(0, await queue.TakeAsync(10));
    }

    [TestMethod]
    public async Task QueueRejectsOperationReuseWithDifferentPayload()
    {
        var store = new InMemoryMirrorPulseUploadQueueStore();
        var queue = new MirrorPulseUploadDispatcher(store);
        var operationId = Guid.NewGuid();
        var instance = InstanceId.New();

        await queue.EnqueueAsync(new MirrorPulseUploadRequest(
            operationId, instance, "docs/report.txt", new byte[] { 1 }, DateTimeOffset.UtcNow));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => queue.EnqueueAsync(
            new MirrorPulseUploadRequest(
                operationId, instance, "docs/report.txt", new byte[] { 2 }, DateTimeOffset.UtcNow)).AsTask());
    }
}

using System.Text.Json;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseInstanceSchedulerTests
{
    [TestMethod]
    public async Task SameInstanceWaitsWhileAnotherInstanceAndWorkerResponseReaderRemainFree()
    {
        await using var scheduler = new MirrorPulseInstanceScheduler();
        InstanceId first = InstanceId.New();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<int> poll = scheduler.RunAsync(first, async token =>
        {
            entered.SetResult();
            await release.Task.WaitAsync(token);
            return 1;
        }).AsTask();
        await entered.Task;
        Task<int> refresh = scheduler.RunAsync(first, _ =>
        {
            secondEntered.SetResult();
            return ValueTask.FromResult(2);
        }).AsTask();
        using var cancel = new CancellationTokenSource();
        Task<int> canceled = scheduler.RunAsync(first, _ => ValueTask.FromResult(3), cancel.Token).AsTask();
        cancel.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => canceled);
        Assert.AreEqual(4, await scheduler.RunAsync(InstanceId.New(), _ => ValueTask.FromResult(4)));
        var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var inbox = new AdapterWorkerRemoteBatchInbox(async (_, token) =>
        {
            await scheduler.RunAsync(first, _ =>
            {
                delivered.SetResult();
                return ValueTask.FromResult(true);
            }, token);
        }, CancellationToken.None);
        inbox.Post(JsonSerializer.SerializeToElement(new { batch = 1 }));
        // Post returns while apply is waiting: the caller can read the response needed by poll.
        Assert.IsFalse(secondEntered.Task.IsCompleted);
        Assert.IsFalse(delivered.Task.IsCompleted);
        release.SetResult();
        Assert.AreEqual(1, await poll);
        Assert.AreEqual(2, await refresh);
        await delivered.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [TestMethod]
    public async Task ShutdownCancelsActiveAndQueuedWorkAndDrainsBeforeDisposal()
    {
        var scheduler = new MirrorPulseInstanceScheduler();
        InstanceId instance = InstanceId.New();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<bool> active = scheduler.RunAsync(instance, async token =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return true;
        }).AsTask();
        await entered.Task;
        Task<bool> queued = scheduler.RunAsync(instance, _ => ValueTask.FromResult(true)).AsTask();
        await scheduler.DisposeAsync();
        await Assert.ThrowsAsync<OperationCanceledException>(() => active);
        await Assert.ThrowsAsync<OperationCanceledException>(() => queued);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
            scheduler.RunAsync(instance, _ => ValueTask.FromResult(true)).AsTask());
    }

    [TestMethod]
    public async Task InboxFailureCancelsReaderAndIsReportedOnDispose()
    {
        var inbox = new AdapterWorkerRemoteBatchInbox((_, _) => throw new IOException("fixture apply failure"), CancellationToken.None);
        inbox.Post(JsonSerializer.SerializeToElement(new { }));
        await Assert.ThrowsAsync<OperationCanceledException>(() => Task.Delay(Timeout.InfiniteTimeSpan, inbox.CancellationToken));
        await Assert.ThrowsExactlyAsync<IOException>(() => inbox.DisposeAsync().AsTask());
    }
}

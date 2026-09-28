using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseLocalChangeFeedSessionTests
{
    [TestMethod]
    public async Task StartIsIdempotentAndReadReturnsNativeBatch()
    {
        var runtime = new FakeRuntime();
        await using var session = new MirrorPulseLocalChangeFeedSession(runtime);

        await session.StartAsync();
        await session.StartAsync();
        var batch = await session.ReadBatchAsync();

        Assert.IsTrue(session.IsStarted);
        Assert.AreEqual(1, runtime.StartCount);
        Assert.IsNull(batch);
        Assert.AreEqual(1, runtime.ReadCount);
    }

    [TestMethod]
    public async Task ReadBeforeStartFailsWithoutTouchingRuntime()
    {
        var runtime = new FakeRuntime();
        await using var session = new MirrorPulseLocalChangeFeedSession(runtime);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => ReadAsync(session));
        Assert.AreEqual(0, runtime.ReadCount);
    }

    private sealed class FakeRuntime : IMirrorPulseLocalChangeFeedRuntime
    {
        public bool IsStarted { get; private set; }
        public int StartCount { get; private set; }
        public int ReadCount { get; private set; }

        public ValueTask StartAsync(CancellationToken cancellationToken)
        {
            IsStarted = true;
            StartCount++;
            return ValueTask.CompletedTask;
        }

        public ValueTask<CloudLocalChangeBatch> ReadBatchAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return ValueTask.FromResult<CloudLocalChangeBatch>(default!);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static async Task ReadAsync(MirrorPulseLocalChangeFeedSession session) =>
        await session.ReadBatchAsync();
}

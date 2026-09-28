using System.Runtime.Versioning;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseHydrationDemandCallbackTests
{
    [TestMethod]
    public async Task HandleForwardsValidatedDemandToWorkerHandler()
    {
        var handler = new RecordingHandler();
        var callback = new MirrorPulseHydrationDemandCallback(handler);
        var demand = new MirrorPulseHydrationDemand("docs/report.txt", new byte[] { 1, 2 }, 4, 8);

        await using var stream = await callback.HandleAsync(demand);

        Assert.AreSame(demand, handler.Demand);
        Assert.IsNotNull(stream);
    }

    [TestMethod]
    public async Task HandleRejectsInvalidRangesBeforeCallingWorker()
    {
        var handler = new RecordingHandler();
        var callback = new MirrorPulseHydrationDemandCallback(handler);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            callback.HandleAsync(new MirrorPulseHydrationDemand("docs/report.txt", ReadOnlyMemory<byte>.Empty, -1, 1)).AsTask());
        Assert.IsNull(handler.Demand);
    }

    private sealed class RecordingHandler : IMirrorPulseHydrationDemandHandler
    {
        public MirrorPulseHydrationDemand? Demand { get; private set; }

        public ValueTask<Stream> HandleAsync(
            MirrorPulseHydrationDemand demand,
            CancellationToken cancellationToken)
        {
            Demand = demand;
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }
}

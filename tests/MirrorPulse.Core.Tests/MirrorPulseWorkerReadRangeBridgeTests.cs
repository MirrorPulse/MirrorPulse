using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWorkerReadRangeBridgeTests
{
    [TestMethod]
    public async Task OpenReadForwardsBoundInstanceAndRange()
    {
        var instanceId = InstanceId.New();
        var transport = new RecordingTransport();
        var bridge = new MirrorPulseWorkerReadRangeBridge(instanceId, transport);
        var request = new MirrorPulseContentFetchRequest("docs/report.txt", new byte[] { 4, 5 }, 12, 18);

        await using var stream = await bridge.OpenReadAsync(request, CancellationToken.None);

        Assert.IsNotNull(stream);
        Assert.IsNotNull(transport.Request);
        Assert.AreEqual(instanceId, transport.Request.InstanceId);
        Assert.AreEqual(request.NormalizedPath, transport.Request.NormalizedPath);
        Assert.AreEqual(request.Offset, transport.Request.Offset);
        Assert.AreEqual(request.Length, transport.Request.Length);
        CollectionAssert.AreEqual(request.FileIdentity.ToArray(), transport.Request.FileIdentity.ToArray());
    }

    [TestMethod]
    public async Task OpenReadRejectsNegativeOffsetBeforeTransport()
    {
        var transport = new RecordingTransport();
        var bridge = new MirrorPulseWorkerReadRangeBridge(InstanceId.New(), transport);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            bridge.OpenReadAsync(new MirrorPulseContentFetchRequest("file", ReadOnlyMemory<byte>.Empty, -1, 2), CancellationToken.None).AsTask());

        Assert.IsNull(transport.Request);
    }

    private sealed class RecordingTransport : IMirrorPulseWorkerRangeTransport
    {
        public MirrorPulseWorkerReadRangeRequest? Request { get; private set; }

        public ValueTask<Stream> ReadRangeAsync(
            MirrorPulseWorkerReadRangeRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.FromResult<Stream>(new MemoryStream());
        }
    }
}

using System.Runtime.Versioning;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseDemandProviderTests
{
    [TestMethod]
    public async Task SeekedHydrationUsesOnlyMatchingInstanceAndBoundedRange()
    {
        var first = InstanceId.New();
        var second = InstanceId.New();
        var transport = new RecordingRangeTransport([10, 11, 12, 13, 14, 15, 16, 17]);
        var provider = new MirrorPulseDemandProvider([first, second], transport);
        byte[] identity = MirrorPulsePlaceholderIdentity.Create(second, "same-remote-id", "v2").Encode();

        await using Stream source = await provider.OpenReadAsync("Second/report.bin", identity, 8, 4, 3);
        Assert.IsTrue(source.CanSeek);
        Assert.AreEqual(4, source.Seek(4, SeekOrigin.Begin));
        var buffer = new byte[3];
        int read = await source.ReadAsync(buffer);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 14, 15, 16 }, buffer);
        Assert.HasCount(1, transport.Requests);
        Assert.AreEqual(second, transport.Requests[0].InstanceId);
        Assert.AreEqual("Second/report.bin", transport.Requests[0].NormalizedPath);
        Assert.AreEqual(4, transport.Requests[0].Offset);
        Assert.AreEqual(3, transport.Requests[0].Length);
        CollectionAssert.AreEqual(identity, transport.Requests[0].FileIdentity.ToArray());
        Assert.AreEqual(1, transport.DisposedStreams);
    }

    [TestMethod]
    public async Task UnknownInstanceIdentityNeverReachesWorker()
    {
        var transport = new RecordingRangeTransport([1, 2, 3]);
        var provider = new MirrorPulseDemandProvider([InstanceId.New()], transport);
        byte[] identity = MirrorPulsePlaceholderIdentity.Create(InstanceId.New(), "file").Encode();

        await Assert.ThrowsExactlyAsync<FileNotFoundException>(() =>
            provider.OpenReadAsync("Other/file", identity, 3, 0, 1).AsTask());
        Assert.IsEmpty(transport.Requests);
    }

    [TestMethod]
    public async Task TruncatedWorkerRangeFailsWithoutAdvancingStream()
    {
        var instance = InstanceId.New();
        var transport = new RecordingRangeTransport([1, 2]) { Truncate = true };
        var provider = new MirrorPulseDemandProvider([instance], transport);
        byte[] identity = MirrorPulsePlaceholderIdentity.Create(instance, "file").Encode();
        await using Stream source = await provider.OpenReadAsync("file", identity, 5, 0, 4);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() =>
            source.ReadAsync(new byte[4]).AsTask());
        Assert.AreEqual(0, source.Position);
        Assert.AreEqual(1, transport.DisposedStreams);
    }

    private sealed class RecordingRangeTransport(byte[] content) : IMirrorPulseWorkerRangeTransport
    {
        public List<MirrorPulseWorkerReadRangeRequest> Requests { get; } = [];

        public bool Truncate { get; init; }

        public int DisposedStreams { get; private set; }

        public ValueTask<Stream> ReadRangeAsync(
            MirrorPulseWorkerReadRangeRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            int count = Truncate ? Math.Min(2, checked((int)request.Length)) : checked((int)request.Length);
            byte[] bytes = content.AsSpan(checked((int)request.Offset), count).ToArray();
            return ValueTask.FromResult<Stream>(new RecordingStream(bytes, () => DisposedStreams++));
        }
    }

    private sealed class RecordingStream(byte[] content, Action onDispose) : MemoryStream(content, writable: false)
    {
        public override ValueTask DisposeAsync()
        {
            onDispose();
            return base.DisposeAsync();
        }
    }
}

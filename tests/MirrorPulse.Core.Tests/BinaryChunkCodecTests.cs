using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class BinaryChunkCodecTests
{
    [TestMethod]
    public void CodecRoundTripsChunkMetadataAndPayload()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        var digest = Sha256Digest.Compute(data);
        var frame = new BinaryChunkFrame(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            Guid.NewGuid(),
            42,
            data,
            true,
            digest);

        var decoded = BinaryChunkCodec.Decode(BinaryChunkCodec.Encode(frame));

        Assert.AreEqual(frame.RequestId, decoded.RequestId);
        Assert.AreEqual(frame.InstanceId, decoded.InstanceId);
        Assert.AreEqual(frame.WorkerSessionId, decoded.WorkerSessionId);
        Assert.AreEqual(frame.StreamId, decoded.StreamId);
        Assert.AreEqual(frame.Offset, decoded.Offset);
        Assert.IsTrue(frame.Data.Span.SequenceEqual(decoded.Data.Span));
        Assert.AreEqual(frame.EndOfStream, decoded.EndOfStream);
        Assert.AreEqual(frame.Sha256, decoded.Sha256);
    }

    [TestMethod]
    public void CodecRejectsInvalidFrameShape()
    {
        Assert.ThrowsExactly<ArgumentException>(() => BinaryChunkCodec.Decode(new byte[10]));

        var frame = new BinaryChunkFrame(Guid.NewGuid(), InstanceId.New(), WorkerSessionId.New(), Guid.NewGuid(), 0, new byte[] { 1 }, false);
        var encoded = BinaryChunkCodec.Encode(frame);
        encoded[76] = 0x80;

        Assert.ThrowsExactly<ArgumentException>(() => BinaryChunkCodec.Decode(encoded));
    }
}

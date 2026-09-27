using System.Text.Json;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ControlFrameJsonCodecTests
{
    [TestMethod]
    public void CodecRoundTripsUtf8Envelope()
    {
        using var document = JsonDocument.Parse("{\"state\":\"ready\",\"roots\":2}");
        var envelope = new ControlFrameEnvelope(
            1,
            "health",
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            true,
            document.RootElement);

        var bytes = ControlFrameJsonCodec.Encode(envelope);
        var decoded = ControlFrameJsonCodec.Decode(bytes);

        Assert.AreEqual(envelope.ProtocolVersion, decoded.ProtocolVersion);
        Assert.AreEqual(envelope.MessageType, decoded.MessageType);
        Assert.AreEqual(envelope.RequestId, decoded.RequestId);
        Assert.AreEqual(envelope.InstanceId, decoded.InstanceId);
        Assert.AreEqual(envelope.WorkerSessionId, decoded.WorkerSessionId);
        Assert.AreEqual(envelope.IsResponse, decoded.IsResponse);
        Assert.AreEqual(envelope.Payload.GetProperty("state").GetString(), decoded.Payload.GetProperty("state").GetString());
        Assert.AreEqual(envelope.Payload.GetProperty("roots").GetInt32(), decoded.Payload.GetProperty("roots").GetInt32());
    }

    [TestMethod]
    public void CodecRejectsEmptyDocument()
    {
        Assert.ThrowsExactly<JsonException>(() => ControlFrameJsonCodec.Decode(ReadOnlySpan<byte>.Empty));
    }
}

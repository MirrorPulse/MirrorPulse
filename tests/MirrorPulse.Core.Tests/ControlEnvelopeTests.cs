using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ControlEnvelopeTests
{
    [TestMethod]
    public void EnvelopeCarriesAddressingAndClonedJsonPayload()
    {
        using var document = JsonDocument.Parse("{\"enabled\":true}");
        var envelope = new ControlFrameEnvelope(
            1,
            "Configure",
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            isResponse: false,
            document.RootElement);

        Assert.AreEqual("Configure", envelope.MessageType);
        Assert.IsFalse(envelope.IsResponse);
        Assert.IsTrue(envelope.Payload.GetProperty("enabled").GetBoolean());
    }

    [TestMethod]
    public void EnvelopeRejectsInvalidVersionAndRequestId()
    {
        using var document = JsonDocument.Parse("{}");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ControlFrameEnvelope(
            0,
            "Hello",
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            false,
            document.RootElement));
        Assert.ThrowsExactly<ArgumentException>(() => new ControlFrameEnvelope(
            1,
            "Hello",
            Guid.Empty,
            InstanceId.New(),
            WorkerSessionId.New(),
            false,
            document.RootElement));
    }
}

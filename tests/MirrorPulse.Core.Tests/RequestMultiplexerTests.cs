using System.Text.Json;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class RequestMultiplexerTests
{
    [TestMethod]
    public async Task MultiplexerCompletesTheMatchingRequest()
    {
        using var multiplexer = new RequestMultiplexer();
        var requestId = Guid.NewGuid();
        var task = multiplexer.Register(requestId);
        var response = CreateEnvelope(requestId);

        Assert.IsTrue(multiplexer.TryComplete(response));
        Assert.AreSame(response, await task);
        Assert.AreEqual(0, multiplexer.PendingCount);
        Assert.IsFalse(multiplexer.TryComplete(response));
    }

    [TestMethod]
    public async Task MultiplexerCancelsPendingRequest()
    {
        using var multiplexer = new RequestMultiplexer();
        var requestId = Guid.NewGuid();
        var task = multiplexer.Register(requestId);

        Assert.ThrowsExactly<InvalidOperationException>(() => multiplexer.Register(requestId));
        Assert.IsTrue(multiplexer.Cancel(requestId));
        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => task);
        Assert.IsFalse(multiplexer.Cancel(requestId));
    }

    private static ControlFrameEnvelope CreateEnvelope(Guid requestId) => new(
        1,
        "response",
        requestId,
        InstanceId.New(),
        WorkerSessionId.New(),
        true,
        JsonDocument.Parse("{}").RootElement);
}

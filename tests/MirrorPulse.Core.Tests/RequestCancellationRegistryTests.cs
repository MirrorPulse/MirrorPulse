using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class RequestCancellationRegistryTests
{
    [TestMethod]
    public void CallerCancellationPropagatesToRequestToken()
    {
        using var registry = new RequestCancellationRegistry();
        using var caller = new CancellationTokenSource();
        var requestId = Guid.NewGuid();
        var token = registry.Register(requestId, caller.Token);

        caller.Cancel();

        Assert.IsTrue(token.IsCancellationRequested);
        Assert.AreEqual(1, registry.Count);
        Assert.IsTrue(registry.Remove(requestId));
        Assert.AreEqual(0, registry.Count);
    }

    [TestMethod]
    public void ExplicitCancellationRemovesRequest()
    {
        using var registry = new RequestCancellationRegistry();
        var requestId = Guid.NewGuid();
        var token = registry.Register(requestId);

        Assert.ThrowsExactly<InvalidOperationException>(() => registry.Register(requestId));
        Assert.IsTrue(registry.Cancel(requestId));
        Assert.IsTrue(token.IsCancellationRequested);
        Assert.IsFalse(registry.Cancel(requestId));
    }
}

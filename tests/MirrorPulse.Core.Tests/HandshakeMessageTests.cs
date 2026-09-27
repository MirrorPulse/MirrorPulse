using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class HandshakeMessageTests
{
    [TestMethod]
    public void HelloAndReadyCarryTheSameWorkerSessionContext()
    {
        var instanceId = InstanceId.New();
        var sessionId = WorkerSessionId.New();
        var offer = new WorkerProtocolOffer(
            AdapterId.Parse("example.webdav"),
            "1.0.0",
            sessionId,
            "win-x64",
            new ProtocolVersionRange(1, 1),
            Sha256Digest.Parse(new string('E', 64)));
        var hello = new HelloMessage(Guid.NewGuid(), instanceId, offer);
        var ready = new ReadyMessage(
            hello.RequestId,
            instanceId,
            sessionId,
            new WorkerProtocolSelection(true, 1),
            Array.Empty<RootRegistration>());

        Assert.AreEqual("Hello", hello.MessageType);
        Assert.AreEqual("Ready", ready.MessageType);
        Assert.AreEqual(hello.WorkerSessionId, ready.WorkerSessionId);
        Assert.IsTrue(ready.Selection.Accepted);
        Assert.HasCount(0, ready.Roots);
    }

    [TestMethod]
    public void ReadyCopiesRootRegistrations()
    {
        var roots = new List<RootRegistration> { CreateRoot() };
        var ready = new ReadyMessage(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            new WorkerProtocolSelection(true, 1),
            roots);
        roots.Clear();

        Assert.HasCount(1, ready.Roots);
    }

    private static RootRegistration CreateRoot() => new(
        AdapterId.Parse("example.webdav"),
        InstanceId.New(),
        RootId.New(),
        "example.webdav:documents",
        "Documents",
        "Documents",
        customEntry: false,
        state: RootRegistrationState.Active,
        registeredAt: DateTimeOffset.UtcNow);
}

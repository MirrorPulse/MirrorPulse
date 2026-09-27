using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerHandshakeServiceTests
{
    [TestMethod]
    public void ServiceSelectsHighestCommonVersionAndCopiesRoots()
    {
        var root = CreateRoot();
        var service = new WorkerHandshakeService(new ProtocolVersionRange(1, 3), new[] { root });
        var hello = CreateHello(new ProtocolVersionRange(2, 4));

        var result = service.HandleHello(hello);

        Assert.IsTrue(result.Selection.Accepted);
        Assert.AreEqual(3, result.Selection.SelectedVersion);
        Assert.IsNotNull(result.Ready);
        Assert.AreEqual(hello.WorkerSessionId, result.Ready.WorkerSessionId);
        Assert.AreEqual(root, result.Ready.Roots.Single());
    }

    [TestMethod]
    public void ServiceRejectsDisjointProtocolRanges()
    {
        var service = new WorkerHandshakeService(new ProtocolVersionRange(1, 2), Array.Empty<RootRegistration>());

        var result = service.HandleHello(CreateHello(new ProtocolVersionRange(3, 4)));

        Assert.IsFalse(result.Selection.Accepted);
        Assert.AreEqual(ErrorCodes.ProtocolVersionUnsupported, result.Selection.RejectionCode);
        Assert.IsNull(result.Ready);
    }

    private static HelloMessage CreateHello(ProtocolVersionRange versions) => new(
        Guid.NewGuid(),
        InstanceId.New(),
        new WorkerProtocolOffer(
            new AdapterId("sample.adapter"),
            "1.0.0",
            WorkerSessionId.New(),
            "win-x64",
            versions,
            Sha256Digest.Compute(new byte[] { 1 })));

    private static RootRegistration CreateRoot() => new(
        new AdapterId("sample.adapter"),
        InstanceId.New(),
        RootId.New(),
        "sample-root",
        "Sample",
        "Sample",
        false,
        RootRegistrationState.Active,
        DateTimeOffset.UtcNow);
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class ProtocolNegotiationTests
{
    [TestMethod]
    public void WorkerOfferCarriesSessionVersionAndManifestIdentity()
    {
        var offer = new WorkerProtocolOffer(
            AdapterId.Parse("example.webdav"),
            "1.0.0",
            WorkerSessionId.New(),
            "win-x64",
            new ProtocolVersionRange(1, 2),
            Sha256Digest.Parse(new string('D', 64)));

        Assert.AreEqual(1, offer.SupportedVersions.Minimum);
        Assert.AreEqual("win-x64", offer.RuntimeIdentifier);
        Assert.AreEqual(new string('D', 64), offer.ManifestSha256.Hexadecimal);
    }

    [TestMethod]
    public void SelectionRequiresVersionOnSuccessAndReasonOnFailure()
    {
        var accepted = new WorkerProtocolSelection(true, 1);
        var rejected = new WorkerProtocolSelection(false, null, "protocol.noCommonVersion");

        Assert.IsTrue(accepted.Accepted);
        Assert.AreEqual(1, accepted.SelectedVersion);
        Assert.IsFalse(rejected.Accepted);
        Assert.AreEqual("protocol.noCommonVersion", rejected.RejectionCode);
        Assert.ThrowsExactly<ArgumentException>(() => new WorkerProtocolSelection(true, null));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkerProtocolSelection(false, null));
    }
}

using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulsePlaceholderIdentityMapperTests
{
    [TestMethod]
    public void MappingIsStablePerInstanceAndRoundTripsThroughCfSharpEncoding()
    {
        var firstInstance = new InstanceId(Guid.Parse("f7f2d1c4-3fc6-4f56-b4c7-3bb4c1f16e02"));
        var secondInstance = new InstanceId(Guid.Parse("e2a5b269-9a7b-4fc7-a22c-8e2f56f2f747"));
        var first = MirrorPulsePlaceholderIdentity.Create(firstInstance, "remote-42", "rev-7");
        var repeated = MirrorPulsePlaceholderIdentity.Create(firstInstance, "remote-42", "rev-7");
        var isolated = MirrorPulsePlaceholderIdentity.Create(secondInstance, "remote-42", "rev-7");

        Assert.AreEqual(first.ToCfSharp().ItemId, repeated.ToCfSharp().ItemId);
        Assert.AreNotEqual(first.ToCfSharp().ItemId, isolated.ToCfSharp().ItemId);

        var decoded = MirrorPulsePlaceholderIdentity.Decode(firstInstance, first.Encode());
        Assert.AreEqual(first.RemoteId, decoded.RemoteId);
        Assert.AreEqual(first.RemoteRevision, decoded.RemoteRevision);
        Assert.AreEqual(first.ToCfSharp().ItemId, decoded.ToCfSharp().ItemId);
    }
}

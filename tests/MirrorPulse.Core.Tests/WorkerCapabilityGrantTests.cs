using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerCapabilityGrantTests
{
    private static readonly string[] ExampleHost = ["Example.com"];
    private static readonly string[] InvalidHost = ["not a host"];

    [TestMethod]
    public void NetworkPolicyAllowsDeclaredHostsOnly()
    {
        var policy = new NetworkAccessPolicy(true, ExampleHost);
        var grant = new WorkerCapabilityGrant(InstanceId.New(), new AdapterCapabilities(true, true, true, false), policy);

        Assert.IsTrue(grant.Network.Allows(new Uri("https://example.com/path")));
        Assert.IsFalse(grant.Network.Allows(new Uri("https://other.example.com/path")));
    }

    [TestMethod]
    public void NetworkPolicyRejectsUndeclaredAccessAndInvalidHosts()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new NetworkAccessPolicy(false, ExampleHost));
        Assert.ThrowsExactly<ArgumentException>(() => new NetworkAccessPolicy(true, InvalidHost));
        Assert.ThrowsExactly<ArgumentException>(() => new WorkerCapabilityGrant(
            InstanceId.New(),
            new AdapterCapabilities(false, true, false, false),
            new NetworkAccessPolicy(true)));
    }
}

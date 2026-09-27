using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class IdentityTests
{
    [TestMethod]
    public void AdapterIdNormalizesCaseAndPreservesStableValue()
    {
        var adapterId = AdapterId.Parse("Example.WebDAV");

        Assert.AreEqual("example.webdav", adapterId.Value);
        Assert.AreEqual("example.webdav", adapterId.ToString());
    }

    [TestMethod]
    public void AdapterIdRejectsInvalidCharacters()
    {
        Assert.IsFalse(AdapterId.TryParse("example/webdav", out _));
        Assert.ThrowsExactly<ArgumentException>(() => AdapterId.Parse(" example.webdav"));
    }

    [TestMethod]
    public void GeneratedScopedIdsAreNonEmptyAndRoundTrip()
    {
        var ids = new object[]
        {
            InstallId.New(),
            InstanceId.New(),
            WorkerSessionId.New(),
            RootId.New()
        };

        foreach (var id in ids)
        {
            var value = id switch
            {
                InstallId installId => installId.Value,
                InstanceId instanceId => instanceId.Value,
                WorkerSessionId sessionId => sessionId.Value,
                RootId rootId => rootId.Value,
                _ => Guid.Empty
            };

            Assert.AreNotEqual(Guid.Empty, value);
        }

        var instance = InstanceId.New();
        Assert.AreEqual(instance, InstanceId.Parse(instance.ToString()));
    }
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class RootRegistrationTests
{
    [TestMethod]
    public void OneInstanceCanOwnMultipleDistinctRoots()
    {
        var instanceId = InstanceId.New();
        var first = CreateRoot(instanceId, RootId.New(), "Documents", "documents");
        var second = CreateRoot(instanceId, RootId.New(), "Photos", "photos");

        Assert.AreEqual(first.InstanceId, second.InstanceId);
        Assert.AreNotEqual(first.RootId, second.RootId);
        Assert.AreNotEqual(first.UniquenessKey, second.UniquenessKey);
        Assert.AreEqual(RootRegistrationState.Active, first.State);
    }

    [TestMethod]
    public void RootRegistrationRejectsNestedDirectoryNames()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateRoot(InstanceId.New(), RootId.New(), "Invalid", "folder\\nested"));
        Assert.ThrowsExactly<ArgumentException>(() => CreateRoot(InstanceId.New(), RootId.New(), "Invalid", "folder/child"));
    }

    private static RootRegistration CreateRoot(InstanceId instanceId, RootId rootId, string label, string directoryName) => new(
        AdapterId.Parse("example.webdav"),
        instanceId,
        rootId,
        $"example.webdav:{directoryName}",
        label,
        directoryName,
        customEntry: false,
        state: RootRegistrationState.Active,
        registeredAt: new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero));
}

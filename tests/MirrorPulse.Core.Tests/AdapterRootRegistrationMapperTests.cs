using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterRootRegistrationMapperTests
{
    [TestMethod]
    public void MapperPreservesMultipleAdapterRootsAndCustomEntryPolicy()
    {
        var instanceId = InstanceId.New();
        var roots = AdapterRootRegistrationMapper.MapAll(
            AdapterId.Parse("example.webdav"),
            instanceId,
            [
                new AdapterRootDefinition("documents", "Documents", "Documents", false),
                new AdapterRootDefinition("custom", "Custom", "Custom", true)
            ]);

        Assert.HasCount(2, roots);
        Assert.AreEqual(instanceId, roots[0].InstanceId);
        Assert.IsTrue(roots[1].CustomEntry);
        Assert.AreNotEqual(roots[0].RootId, roots[1].RootId);
    }

    [TestMethod]
    public void MapperRejectsDuplicateAdapterRootKeys()
    {
        var definitions = new[]
        {
            new AdapterRootDefinition("same", "One", "one", false),
            new AdapterRootDefinition("SAME", "Two", "two", false)
        };

        Assert.ThrowsExactly<InvalidOperationException>(() => AdapterRootRegistrationMapper.MapAll(
            AdapterId.Parse("example.webdav"),
            InstanceId.New(),
            definitions));
    }
}

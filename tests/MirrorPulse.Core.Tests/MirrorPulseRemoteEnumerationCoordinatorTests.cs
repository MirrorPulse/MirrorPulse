using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseRemoteEnumerationCoordinatorTests
{
    [TestMethod]
    public async Task CoordinatorFollowsOpaqueCursorsAndAggregatesPages()
    {
        var pages = new Queue<CloudRemoteDirectoryPage>([
            CreatePage("one.txt", new byte[] { 1 }, isComplete: false),
            CreatePage("two.txt", ReadOnlyMemory<byte>.Empty, isComplete: true)]);
        var queries = new List<CloudRemoteDirectoryQuery>();
        var coordinator = new MirrorPulseRemoteEnumerationCoordinator((query, cancellationToken) =>
        {
            queries.Add(query);
            return ValueTask.FromResult(pages.Dequeue());
        });

        var result = await coordinator.ReadAllAsync("Documents", 50);

        Assert.AreEqual(2, result.PagesRead);
        Assert.HasCount(2, result.Entries);
        CollectionAssert.AreEqual(new byte[] { 1 }, queries[1].ContinuationCursor.ToArray());
    }

    [TestMethod]
    public async Task CoordinatorRejectsRepeatedContinuationCursor()
    {
        var page = CreatePage("one.txt", new byte[] { 1 }, isComplete: false);
        var coordinator = new MirrorPulseRemoteEnumerationCoordinator((query, cancellationToken) =>
            ValueTask.FromResult(page));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(() => coordinator.ReadAllAsync("", 50));
    }

    private static CloudRemoteDirectoryPage CreatePage(string path, ReadOnlyMemory<byte> cursor, bool isComplete)
    {
        var metadata = CloudPlaceholderMetadata.CreateFileBuilder().Build();
        var entry = new CloudRemoteDirectoryEntry("remote-" + path, "rev-1", CloudItemKind.File, path, null, 3, metadata, false);
        return new CloudRemoteDirectoryPage([entry], cursor, isComplete);
    }
}

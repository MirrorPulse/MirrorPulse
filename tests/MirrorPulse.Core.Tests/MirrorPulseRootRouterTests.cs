using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRootRouterTests
{
    private static readonly string[] ExpectedTopLevelNames = ["Backup", "Documents", "Photos"];

    [TestMethod]
    public async Task OneSyncRootListsMultipleCopiesAndMultipleRootsPerInstance()
    {
        var first = InstanceId.New();
        var second = InstanceId.New();
        string syncRoot = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var roots = AdapterRootRegistrationMapper.MapAll(
            AdapterId.Parse("example.drive"), first,
            [new AdapterRootDefinition("docs", "Documents", "Documents", false),
             new AdapterRootDefinition("photos", "Photos", "Photos", false)],
            RootRegistrationState.Active).Concat(AdapterRootRegistrationMapper.MapAll(
                AdapterId.Parse("example.drive"), second,
                [new AdapterRootDefinition("backup", "Backup", "Backup", false)],
                RootRegistrationState.Active));
        var router = new MirrorPulseRootRouter(syncRoot, roots);
        var source = new RecordingDirectorySource();
        var transport = new RecordingRangeTransport();
        var provider = new MirrorPulseDemandProvider(router, transport, source);

        CloudProviderDirectoryPage top = await provider.FetchChildrenAsync(syncRoot, ReadOnlyMemory<byte>.Empty, null);
        CollectionAssert.AreEqual(ExpectedTopLevelNames, top.Children.Select(child => child.Name).ToArray());
        Assert.IsTrue(top.IsComplete);
        Assert.AreEqual(3, top.Children.Select(child => child.Identity.ItemId).Distinct().Count());

        CloudPlaceholderSpec documents = top.Children.Single(child => child.Name == "Documents");
        CloudProviderDirectoryPage page = await provider.FetchChildrenAsync(
            Path.Combine(syncRoot, "Documents"), documents.Identity.Encode(), null);
        Assert.IsEmpty(page.Children);
        Assert.AreEqual(first, source.InstanceId);
        Assert.AreEqual(string.Empty, source.RelativePath);

        byte[] fileIdentity = MirrorPulsePlaceholderIdentity.Create(second, "file-1").Encode();
        await using Stream stream = await provider.OpenReadAsync(
            Path.Combine(syncRoot, "Backup", "sub", "file.txt"), fileIdentity, 3, 0, 2);
        byte[] bytes = new byte[2];
        await stream.ReadExactlyAsync(bytes);
        Assert.AreEqual(second, transport.Request?.InstanceId);
        Assert.AreEqual(Path.Combine("sub", "file.txt"), transport.Request?.NormalizedPath);
    }

    [TestMethod]
    public void DuplicateTopLevelLabelAcrossInstancesIsRejected()
    {
        var first = CreateRoot(InstanceId.New(), "Shared");
        var second = CreateRoot(InstanceId.New(), "shared");

        Assert.ThrowsExactly<InvalidDataException>(() => new MirrorPulseRootRouter(
            Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N")),
            [first, second]));
    }

    [TestMethod]
    public void WrongInstanceIdentityCannotCrossFirstLevelDirectory()
    {
        var first = InstanceId.New();
        var second = InstanceId.New();
        string syncRoot = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var router = new MirrorPulseRootRouter(syncRoot, [CreateRoot(first, "One"), CreateRoot(second, "Two")]);
        byte[] identity = MirrorPulsePlaceholderIdentity.Create(second, "file").Encode();

        Assert.ThrowsExactly<InvalidDataException>(() => router.Resolve(Path.Combine(syncRoot, "One", "file"), identity));
        Assert.ThrowsExactly<InvalidDataException>(() => router.Resolve(Path.Combine(syncRoot, "..", "outside"), identity));
    }

    private static RootRegistration CreateRoot(InstanceId instanceId, string label) =>
        AdapterRootRegistrationMapper.Map(
            AdapterId.Parse("example.drive"), instanceId,
            new AdapterRootDefinition(label, label, label, false),
            RootRegistrationState.Active);

    private sealed class RecordingDirectorySource : IMirrorPulseDirectoryPageSource
    {
        public InstanceId InstanceId { get; private set; }

        public string? RelativePath { get; private set; }

        public ValueTask<CloudRemoteDirectoryPage> ReadPageAsync(
            InstanceId instanceId,
            string normalizedPath,
            ReadOnlyMemory<byte> continuationCursor,
            int pageSize,
            CancellationToken cancellationToken)
        {
            InstanceId = instanceId;
            RelativePath = normalizedPath;
            return ValueTask.FromResult(new CloudRemoteDirectoryPage([]));
        }
    }

    private sealed class RecordingRangeTransport : IMirrorPulseWorkerRangeTransport
    {
        public MirrorPulseWorkerReadRangeRequest? Request { get; private set; }

        public ValueTask<Stream> ReadRangeAsync(
            MirrorPulseWorkerReadRangeRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.FromResult<Stream>(new MemoryStream([1, 2], writable: false));
        }
    }
}

using System.Runtime.Versioning;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseContentProviderBridgeTests
{
    [TestMethod]
    public async Task BridgeForwardsPathIdentityAndRangeToAdapterReader()
    {
        var reader = new RecordingReader();
        var bridge = new MirrorPulseContentProviderBridge(reader);
        var request = new MirrorPulseContentFetchRequest("Documents/report.pdf", new byte[] { 7, 8 }, 12, 64);

        await using var stream = await bridge.OpenReadAsync(request);

        Assert.AreEqual(request, reader.Request);
        Assert.AreEqual(0, stream.Position);
    }

    private sealed class RecordingReader : IMirrorPulseContentReader
    {
        public MirrorPulseContentFetchRequest? Request { get; private set; }

        public ValueTask<Stream> OpenReadAsync(MirrorPulseContentFetchRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return ValueTask.FromResult<Stream>(new MemoryStream([1, 2, 3], writable: false));
        }
    }
}

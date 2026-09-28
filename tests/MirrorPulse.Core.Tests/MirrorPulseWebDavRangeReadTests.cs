using System.Net;
using System.Net.Http.Headers;
using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavRangeReadTests
{
    [TestMethod]
    public async Task ClientSendsRangeAndReturnsContentMetadata()
    {
        var handler = new RangeHandler();
        using var client = new HttpClient(handler);
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        var result = await directory.ReadRangeAsync("file.bin", 2, 4);

        CollectionAssert.AreEqual(new byte[] { 2, 3, 4, 5 }, result.Content);
        Assert.AreEqual(10, result.TotalLength);
        Assert.AreEqual("\"v2\"", result.ETag);
        Assert.AreEqual("bytes=2-5", handler.Range);
    }

    [TestMethod]
    public async Task ClientRejectsEmptyRanges()
    {
        using var client = new HttpClient(new RangeHandler());
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => directory.ReadRangeAsync("file.bin", 0, 0));
    }

    private sealed class RangeHandler : HttpMessageHandler
    {
        public string? Range { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Range = request.Headers.Range?.ToString();
            var response = new HttpResponseMessage(HttpStatusCode.PartialContent)
            {
                Content = new ByteArrayContent([2, 3, 4, 5]),
            };
            response.Content.Headers.ContentRange = new ContentRangeHeaderValue(2, 5, 10);
            response.Headers.ETag = new EntityTagHeaderValue("\"v2\"");
            return Task.FromResult(response);
        }
    }
}

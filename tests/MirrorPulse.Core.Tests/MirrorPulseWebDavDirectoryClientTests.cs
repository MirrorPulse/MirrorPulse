using System.Net;
using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavDirectoryClientTests
{
    [TestMethod]
    public async Task ClientParsesDirectoryAndFilePropertiesFromMultiStatus()
    {
        using var client = new HttpClient(new RecordingHandler(
            """
            <?xml version="1.0" encoding="utf-8" ?>
            <d:multistatus xmlns:d="DAV:">
              <d:response><d:href>/remote/</d:href><d:propstat><d:prop><d:resourcetype><d:collection /></d:resourcetype></d:prop></d:propstat></d:response>
              <d:response><d:href>/remote/docs/</d:href><d:propstat><d:prop><d:resourcetype><d:collection /></d:resourcetype><d:getetag>"docs-v1"</d:getetag></d:prop></d:propstat></d:response>
              <d:response><d:href>/remote/read%20me.txt</d:href><d:propstat><d:prop><d:getcontentlength>7</d:getcontentlength><d:getetag>"file-v2"</d:getetag></d:prop></d:propstat></d:response>
            </d:multistatus>
            """));
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        var entries = await directory.ListDirectoryAsync();

        Assert.HasCount(2, entries);
        Assert.AreEqual("docs/", entries[0].RelativePath);
        Assert.IsTrue(entries[0].IsDirectory);
        Assert.AreEqual("read me.txt", entries[1].RelativePath);
        Assert.AreEqual(7, entries[1].Length);
        Assert.AreEqual("\"file-v2\"", entries[1].ETag);
    }

    [TestMethod]
    public async Task ClientUsesDepthOneAndConfiguredAuthentication()
    {
        var handler = new RecordingHandler("<d:multistatus xmlns:d=\"DAV:\" />");
        using var client = new HttpClient(handler);
        using var credential = MirrorPulseWebDavCredential.CreateBearer("token");
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"), credential);

        await directory.ListDirectoryAsync("docs/");

        Assert.AreEqual("PROPFIND", handler.Method);
        Assert.AreEqual("1", handler.Depth);
        Assert.AreEqual("Bearer token", handler.Authorization);
        StringAssert.Contains(handler.RequestUri!, "/remote/docs/");
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public string? Method { get; private set; }
        public string? Depth { get; private set; }
        public string? Authorization { get; private set; }
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            Depth = request.Headers.GetValues("Depth").Single();
            Authorization = request.Headers.Authorization?.ToString();
            RequestUri = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MultiStatus)
            {
                Content = new StringContent(responseBody),
            });
        }
    }
}

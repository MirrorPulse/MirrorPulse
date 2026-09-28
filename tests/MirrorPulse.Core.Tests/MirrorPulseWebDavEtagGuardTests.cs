using System.Net;
using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavEtagGuardTests
{
    [TestMethod]
    public async Task UploadSendsIfMatchAndReportsAWebDavConflict()
    {
        var handler = new ConflictHandler();
        using var client = new HttpClient(handler);
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        var conflict = await Assert.ThrowsExactlyAsync<MirrorPulseWebDavConflictException>(() =>
            directory.UploadAsync("file.txt", new byte[] { 1 }, "\"v1\""));

        Assert.AreEqual("file.txt", conflict.RelativePath);
        Assert.AreEqual("\"v1\"", conflict.ExpectedETag);
        Assert.AreEqual("\"v2\"", conflict.ActualETag);
        Assert.AreEqual("\"v1\"", handler.IfMatch);
    }

    [TestMethod]
    public void GuardComparesOpaqueTagsWithoutInterpretingTheirContents()
    {
        Assert.IsTrue(MirrorPulseWebDavEtagGuard.Matches("opaque-a", "opaque-a"));
        Assert.IsFalse(MirrorPulseWebDavEtagGuard.Matches("opaque-a", "opaque-b"));
        Assert.IsFalse(MirrorPulseWebDavEtagGuard.Matches(null, "opaque-a"));
    }

    private sealed class ConflictHandler : HttpMessageHandler
    {
        public string? IfMatch { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                IfMatch = request.Headers.IfMatch.Single().Tag;
            }
            else
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            }

            var response = new HttpResponseMessage(HttpStatusCode.PreconditionFailed);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v2\"");
            return Task.FromResult(response);
        }
    }
}

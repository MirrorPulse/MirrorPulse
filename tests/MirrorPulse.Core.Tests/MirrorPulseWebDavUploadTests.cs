using System.Net;
using MirrorPulse.Core.Adapters.WebDav;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseWebDavUploadTests
{
    private static readonly string[] ExpectedPutMoveMethods = ["PUT", "MOVE"];
    private static readonly string[] ExpectedCleanupMethods = ["PUT", "MOVE", "DELETE"];

    [TestMethod]
    public async Task UploadUsesPutThenMoveWithTemporaryResource()
    {
        var handler = new UploadHandler();
        using var client = new HttpClient(handler);
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        var result = await directory.UploadAsync("docs/file.txt", new byte[] { 1, 2, 3 });

        Assert.AreEqual("docs/file.txt", result.RelativePath);
        Assert.AreEqual(3, result.Length);
        CollectionAssert.AreEqual(ExpectedPutMoveMethods, handler.Methods.ToArray());
        StringAssert.Contains(handler.PutUri!, ".mp-upload-");
        Assert.AreEqual("https://dav.example.test/remote/docs/file.txt", handler.Destination);
        Assert.AreEqual("T", handler.Overwrite);
    }

    [TestMethod]
    public async Task UploadCleansTemporaryResourceWhenMoveFails()
    {
        var handler = new UploadHandler { FailMove = true };
        using var client = new HttpClient(handler);
        var directory = new MirrorPulseWebDavDirectoryClient(client, new Uri("https://dav.example.test/remote/"));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => directory.UploadAsync("file.txt", new byte[] { 1 }));

        CollectionAssert.AreEqual(ExpectedCleanupMethods, handler.Methods.ToArray());
    }

    private sealed class UploadHandler : HttpMessageHandler
    {
        public List<string> Methods { get; } = [];
        public string? PutUri { get; private set; }
        public string? Destination { get; private set; }
        public string? Overwrite { get; private set; }
        public bool FailMove { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method.Method);
            if (request.Method == HttpMethod.Put)
            {
                PutUri = request.RequestUri?.ToString();
                _ = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            }
            else if (request.Method.Method == "MOVE")
            {
                Destination = request.Headers.GetValues("Destination").Single();
                Overwrite = request.Headers.GetValues("Overwrite").Single();
                if (FailMove)
                {
                    throw new HttpRequestException("simulated MOVE failure");
                }
            }

            return new HttpResponseMessage(HttpStatusCode.Created);
        }
    }
}

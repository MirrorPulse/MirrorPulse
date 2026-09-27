using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LatestAdapterReleaseParserTests
{
    [TestMethod]
    public void ParserReadsLatestReleaseMetadataAndAssets()
    {
        const string json = """
            {
              "tag_name": "v1.2.3",
              "name": "WebDAV Adapter 1.2.3",
              "html_url": "https://github.com/MirrorPulse/example/releases/tag/v1.2.3",
              "published_at": "2026-09-27T10:00:00Z",
              "assets": [
                {
                  "name": "example.mpadapter",
                  "browser_download_url": "https://github.com/MirrorPulse/example/releases/download/v1.2.3/example.mpadapter",
                  "size": 1234
                }
              ]
            }
            """;

        var release = LatestAdapterReleaseParser.Parse(json);

        Assert.AreEqual("v1.2.3", release.TagName);
        Assert.AreEqual("WebDAV Adapter 1.2.3", release.Name);
        Assert.AreEqual("example.mpadapter", release.Assets.Single().Name);
        Assert.AreEqual(1234, release.Assets.Single().Size);
    }

    [TestMethod]
    public void ParserRejectsNonHttpsAssetUrls()
    {
        const string json = """
            {
              "tag_name": "v1.0.0",
              "html_url": "https://github.com/MirrorPulse/example/releases/tag/v1.0.0",
              "assets": [{
                "name": "example.mpadapter",
                "browser_download_url": "http://example.test/example.mpadapter",
                "size": 1
              }]
            }
            """;

        Assert.ThrowsExactly<System.Text.Json.JsonException>(() => LatestAdapterReleaseParser.Parse(json));
    }
}

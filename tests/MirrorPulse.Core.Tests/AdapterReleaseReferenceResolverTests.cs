using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterReleaseReferenceResolverTests
{
    [TestMethod]
    public void ResolverStoresNormalizedVersionAndPackageUrl()
    {
        var release = LatestAdapterReleaseParser.Parse("""
            {
              "tag_name": "v2.4.0",
              "name": "Adapter 2.4.0",
              "html_url": "https://github.com/MirrorPulse/example/releases/tag/v2.4.0",
              "assets": [{
                "name": "example.mpadapter",
                "browser_download_url": "https://github.com/MirrorPulse/example/releases/download/v2.4.0/example.mpadapter",
                "size": 42
              }]
            }
            """);

        var reference = AdapterReleaseReferenceResolver.Resolve(AdapterId.Parse("example.webdav"), release);

        Assert.AreEqual("2.4.0", reference.Version);
        Assert.AreEqual("example.mpadapter", reference.AssetName);
        Assert.AreEqual("https://github.com/MirrorPulse/example/releases/download/v2.4.0/example.mpadapter", reference.PackageUri.ToString());
    }

    [TestMethod]
    public void ResolverRejectsReleasesWithoutExactlyOnePackageAsset()
    {
        var release = new LatestAdapterRelease(
            "v1.0.0",
            "Adapter",
            new Uri("https://github.com/MirrorPulse/example/releases/tag/v1.0.0"),
            [],
            null);

        Assert.ThrowsExactly<InvalidDataException>(() =>
            AdapterReleaseReferenceResolver.Resolve(AdapterId.Parse("example.webdav"), release));
    }
}

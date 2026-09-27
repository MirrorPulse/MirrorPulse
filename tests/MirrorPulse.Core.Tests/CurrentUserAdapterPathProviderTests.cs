using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Packaging;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CurrentUserAdapterPathProviderTests
{
    [TestMethod]
    public void InstallationPathSeparatesAdapterVersionAndInstallId()
    {
        var root = Path.Combine(Path.GetTempPath(), "mirrorpulse-adapters");
        var provider = new CurrentUserAdapterPathProvider(root);
        var adapterId = AdapterId.Parse("example.webdav");
        var installId = InstallId.New();

        var path = provider.GetInstallationDirectory(adapterId, "1.2.3", installId);

        Assert.AreEqual(Path.Combine(root, "example.webdav", "1.2.3", installId.ToString()), path);
    }

    [TestMethod]
    public void PathProviderRejectsTraversalLikeVersions()
    {
        var provider = new CurrentUserAdapterPathProvider(Path.Combine(Path.GetTempPath(), "mirrorpulse-adapters"));

        Assert.ThrowsExactly<ArgumentException>(() =>
            provider.GetVersionDirectory(AdapterId.Parse("example.webdav"), "../1.2.3"));
    }
}

using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SourceDirectoryPathNormalizerTests
{
    [TestMethod]
    public void NormalizerRemovesTrailingSeparatorsAndAllowsDescendants()
    {
        var root = Path.Combine(Path.GetTempPath(), "mirrorpulse-source");
        var normalized = SourceDirectoryPathNormalizer.Normalize(root + Path.DirectorySeparatorChar);

        Assert.AreEqual(SourceDirectoryPathNormalizer.Normalize(root), normalized);
        Assert.IsTrue(SourceDirectoryPathNormalizer.IsWithin(normalized, Path.Combine(root, "child", "file.txt")));
        Assert.IsFalse(SourceDirectoryPathNormalizer.IsWithin(normalized, root + "-other"));
    }

    [TestMethod]
    public void AuthorizationAllowsOnlyGrantedRightsInsideTheNormalizedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mirrorpulse-source");
        var grants = new SourceDirectoryAuthorizationList(
            InstanceId.New(),
            [new SourceDirectoryGrant(root + Path.DirectorySeparatorChar, SourceDirectoryAccess.Read | SourceDirectoryAccess.Enumerate)]);

        Assert.IsTrue(grants.Allows(Path.Combine(root, "child.txt"), SourceDirectoryAccess.Read));
        Assert.IsFalse(grants.Allows(Path.Combine(root, "child.txt"), SourceDirectoryAccess.Write));
        Assert.IsFalse(grants.Allows(root + "-other", SourceDirectoryAccess.Read));
    }
}

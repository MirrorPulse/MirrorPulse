using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SourceDirectoryAuthorizationTests
{
    private static readonly SourceDirectoryGrant[] Grants =
    [
        new SourceDirectoryGrant("C:\\MirrorPulse\\source", SourceDirectoryAccess.Read | SourceDirectoryAccess.Enumerate),
    ];

    [TestMethod]
    public void AuthorizationListCopiesGrantsAndChecksRights()
    {
        var list = new SourceDirectoryAuthorizationList(InstanceId.New(), Grants);

        Assert.HasCount(1, list.Grants);
        Assert.IsTrue(list.Allows("C:\\MirrorPulse\\source", SourceDirectoryAccess.Read));
        Assert.IsFalse(list.Allows("C:\\MirrorPulse\\source", SourceDirectoryAccess.Write));
    }

    [TestMethod]
    public void AuthorizationListRejectsRelativeAndDuplicateDirectories()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new SourceDirectoryGrant("relative", SourceDirectoryAccess.Read));
        Assert.ThrowsExactly<ArgumentException>(() => new SourceDirectoryGrant("C:\\MirrorPulse\\source", SourceDirectoryAccess.None));

        var duplicate = new[]
        {
            new SourceDirectoryGrant("C:\\MirrorPulse\\source", SourceDirectoryAccess.Read),
            new SourceDirectoryGrant("c:\\mirrorpulse\\SOURCE", SourceDirectoryAccess.Write),
        };
        Assert.ThrowsExactly<ArgumentException>(() => new SourceDirectoryAuthorizationList(InstanceId.New(), duplicate));
    }
}

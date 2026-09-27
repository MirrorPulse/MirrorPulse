using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CredentialTests
{
    [TestMethod]
    public void CredentialReferenceContainsOnlySecureStoreMetadata()
    {
        var reference = new CredentialReference(
            "credential-123",
            CredentialKind.OAuthToken,
            "example.webdav",
            CredentialScope.CurrentUser,
            new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
            "user@example.test");

        Assert.AreEqual(CredentialScope.CurrentUser, reference.Scope);
        Assert.AreEqual("user@example.test", reference.AccountHint);
        Assert.IsNull(typeof(CredentialReference).GetProperty("Secret"));
    }

    [TestMethod]
    public void CredentialReferenceRejectsEmptyIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new CredentialReference(
            "",
            CredentialKind.Password,
            "example",
            CredentialScope.CurrentUser,
            DateTimeOffset.UtcNow));
    }
}

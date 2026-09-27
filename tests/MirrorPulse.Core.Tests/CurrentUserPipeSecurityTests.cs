using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CurrentUserPipeSecurityTests
{
    [TestMethod]
    public void SecurityDescriptorAssignsCurrentUserAsOwner()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var user = identity.User ?? throw new InvalidOperationException("The test process has no Windows user SID.");
        var security = CurrentUserPipeSecurity.Create();
        var descriptor = security.GetSecurityDescriptorSddlForm(AccessControlSections.Owner | AccessControlSections.Access);
        var owner = security.GetOwner(typeof(SecurityIdentifier));

        Assert.IsTrue(descriptor.StartsWith("O:", StringComparison.Ordinal));
        Assert.AreEqual(user, owner);
        Assert.IsTrue(security.GetAccessRules(true, false, typeof(SecurityIdentifier)).Cast<PipeAccessRule>().Any(rule => rule.IdentityReference == user));
    }

    [TestMethod]
    public void SecureFactoryCreatesUnconnectedStream()
    {
        using var server = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions($"mirrorpulse-secure-{Guid.NewGuid():N}"));

        Assert.IsFalse(server.IsConnected);
        Assert.IsTrue(server.CanRead);
        Assert.IsTrue(server.CanWrite);
    }
}

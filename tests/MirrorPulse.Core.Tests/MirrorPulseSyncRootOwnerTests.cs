using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSyncRootOwnerTests
{
    [TestMethod]
    public void OwnerValidatesPersistedPathAndProviderIdentity()
    {
        var owner = new MirrorPulseSyncRootOwner(new FakeOwnerLock());
        var providerId = Guid.Parse("7b6a7d8e-63a5-4b8a-9c6e-1e6e61f95e9d");
        var definition = new MirrorPulseSyncRootDefinition("C:\\MirrorPulse", "0.1.0", providerId, [1]);
        var state = new MirrorPulseSyncRootRegistrationState("c:\\mirrorpulse", providerId, "0.1.0", DateTimeOffset.UtcNow);

        Assert.IsTrue(owner.TryAcquire(TimeSpan.Zero));
        owner.EnsureConsistent(definition, state);

        var mismatch = state with { ProviderVersion = "0.2.0" };
        Assert.ThrowsExactly<InvalidDataException>(() => owner.EnsureConsistent(definition, mismatch));
    }

    private sealed class FakeOwnerLock : ICurrentUserOwnerLock
    {
        public bool IsHeld { get; private set; }

        public bool TryAcquire(TimeSpan timeout)
        {
            IsHeld = true;
            return true;
        }

        public void Release() => IsHeld = false;
    }
}

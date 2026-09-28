using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseSyncRootOwnerTests
{
    [TestMethod]
    public void OwnerRequiresCurrentUserLock()
    {
        var owner = new MirrorPulseSyncRootOwner(new FakeOwnerLock());
        Assert.ThrowsExactly<InvalidOperationException>(owner.EnsureHeld);
        Assert.IsTrue(owner.TryAcquire(TimeSpan.Zero));
        owner.EnsureHeld();
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

using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class CurrentUserOwnerLockTests
{
    [TestMethod]
    public void OwnerLockAllowsOneOwnerAndReacquisitionAfterRelease()
    {
        var scope = $"test-{Guid.NewGuid():N}";
        using var first = CurrentUserOwnerLock.Create(scope);
        using var second = CurrentUserOwnerLock.Create(scope);

        Assert.IsTrue(first.TryAcquire(TimeSpan.Zero));
        Assert.IsFalse(Task.Run(() => second.TryAcquire(TimeSpan.Zero)).GetAwaiter().GetResult());
        first.Release();
        var reacquired = Task.Run(() =>
        {
            var acquired = second.TryAcquire(TimeSpan.FromSeconds(1));
            var held = second.IsHeld;
            second.Release();
            return (acquired, held);
        }).GetAwaiter().GetResult();
        Assert.IsTrue(reacquired.acquired);
        Assert.IsTrue(reacquired.held);
    }

    [TestMethod]
    public void OwnerLockRejectsInvalidTimeout()
    {
        using var owner = CurrentUserOwnerLock.Create($"test-{Guid.NewGuid():N}");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => owner.TryAcquire(TimeSpan.FromMilliseconds(-2)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => owner.TryAcquire(TimeSpan.FromMilliseconds(int.MaxValue) + TimeSpan.FromMilliseconds(1)));
    }

    [TestMethod]
    public void HostLeaseAllowsOnlyOneCurrentUserOwner()
    {
        using var first = MirrorPulseHostLease.CreateDefault();
        using var second = MirrorPulseHostLease.CreateDefault();

        Assert.IsTrue(first.TryAcquire(TimeSpan.Zero));
        Assert.IsFalse(second.TryAcquire(TimeSpan.Zero));
        Assert.IsTrue(first.IsHeld);
        Assert.IsFalse(second.IsHeld);
    }
}

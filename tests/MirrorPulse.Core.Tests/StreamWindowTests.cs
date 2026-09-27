using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class StreamWindowTests
{
    [TestMethod]
    public void WindowBlocksReservationsUntilBytesAreReleased()
    {
        var window = new StreamWindow(10);

        Assert.IsTrue(window.TryReserve(6));
        Assert.IsFalse(window.TryReserve(5));
        Assert.AreEqual(6, window.InFlightBytes);
        Assert.AreEqual(4, window.AvailableBytes);

        window.Release(4);
        Assert.AreEqual(2, window.InFlightBytes);
        Assert.IsTrue(window.TryReserve(8));
        Assert.AreEqual(0, window.AvailableBytes);
    }

    [TestMethod]
    public void WindowRejectsInvalidRelease()
    {
        var window = new StreamWindow(4);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new StreamWindow(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.TryReserve(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => window.Release(1));
    }
}

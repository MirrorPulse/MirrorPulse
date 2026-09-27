using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class TransportConnectionMonitorTests
{
    [TestMethod]
    public void MonitorDetectsInactivityAndReconnect()
    {
        var connectedAt = DateTimeOffset.UtcNow;
        var monitor = new TransportConnectionMonitor(connectedAt, new TransportTimeoutPolicy(TimeSpan.FromSeconds(5)));

        Assert.AreEqual(TransportConnectionState.Connected, monitor.Check(connectedAt.AddSeconds(4)));
        Assert.AreEqual(TransportConnectionState.TimedOut, monitor.Check(connectedAt.AddSeconds(5)));

        monitor.MarkConnected(connectedAt.AddSeconds(6));
        monitor.MarkActivity(connectedAt.AddSeconds(7));
        Assert.AreEqual(TransportConnectionState.Connected, monitor.State);
    }

    [TestMethod]
    public void ExplicitDisconnectWinsOverTimeoutChecks()
    {
        var connectedAt = DateTimeOffset.UtcNow;
        var monitor = new TransportConnectionMonitor(connectedAt, new TransportTimeoutPolicy(TimeSpan.FromSeconds(1)));

        monitor.MarkDisconnected();

        Assert.AreEqual(TransportConnectionState.Disconnected, monitor.Check(connectedAt.AddHours(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => monitor.MarkActivity(connectedAt.AddSeconds(-1)));

        var activeMonitor = new TransportConnectionMonitor(connectedAt, new TransportTimeoutPolicy(TimeSpan.FromSeconds(1)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => activeMonitor.Check(connectedAt.AddSeconds(-1)));
    }

    [TestMethod]
    public void PolicyRejectsNonPositiveTimeout()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TransportTimeoutPolicy(TimeSpan.Zero));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new TransportTimeoutPolicy(TimeSpan.FromSeconds(-1)));
    }
}

using System.Runtime.Versioning;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseFullRescanCoordinatorTests
{
    private static readonly string[] ExpectedEvents = ["rescan", "ack"];

    [TestMethod]
    public async Task RequiredSignalReconcilesBeforeAcknowledgement()
    {
        var events = new List<string>();
        var coordinator = new MirrorPulseFullRescanCoordinator(
            _ =>
            {
                events.Add("rescan");
                return ValueTask.FromResult(7);
            },
            _ =>
            {
                events.Add("ack");
                return ValueTask.CompletedTask;
            });

        var result = await coordinator.HandleAsync(new MirrorPulseLocalChangeSignal(true));

        CollectionAssert.AreEqual(ExpectedEvents, events);
        Assert.IsTrue(result.WasRequired);
        Assert.AreEqual(7, result.ReconciledEntryCount);
    }

    [TestMethod]
    public async Task NormalSignalDoesNotRunRescanOrAcknowledgement()
    {
        var calls = 0;
        var coordinator = new MirrorPulseFullRescanCoordinator(
            _ =>
            {
                calls++;
                return ValueTask.FromResult(1);
            },
            _ =>
            {
                calls++;
                return ValueTask.CompletedTask;
            });

        var result = await coordinator.HandleAsync(new MirrorPulseLocalChangeSignal(false));

        Assert.AreEqual(0, calls);
        Assert.IsFalse(result.WasRequired);
        Assert.AreEqual(0, result.ReconciledEntryCount);
    }
}

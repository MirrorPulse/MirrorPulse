using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseAccountRemovalServiceTests
{
    private static readonly string[] ExpectedOrder = ["unregister", "clear"];

    [TestMethod]
    public async Task RemoveUnregistersBeforeClearingState()
    {
        var events = new List<string>();
        var service = new MirrorPulseAccountRemovalService(
            new RecordingUnregistrar(events),
            _ =>
            {
                events.Add("clear");
                return ValueTask.CompletedTask;
            });

        await service.RemoveAsync("account-root");

        CollectionAssert.AreEqual(ExpectedOrder, events);
    }

    [TestMethod]
    public async Task FailedUnregisterDoesNotClearState()
    {
        var cleared = false;
        var service = new MirrorPulseAccountRemovalService(
            new ThrowingUnregistrar(),
            _ =>
            {
                cleared = true;
                return ValueTask.CompletedTask;
            });

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => service.RemoveAsync("account-root").AsTask());
        Assert.IsFalse(cleared);
    }

    private sealed class RecordingUnregistrar(List<string> events) : IMirrorPulseSyncRootUnregistrar
    {
        public void Unregister(string path) => events.Add("unregister");
    }

    private sealed class ThrowingUnregistrar : IMirrorPulseSyncRootUnregistrar
    {
        public void Unregister(string path) => throw new InvalidOperationException("unregister failed");
    }
}

using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class WorkerEventTests
{
    [TestMethod]
    public void WorkerEventCarriesSessionOrderAndPayload()
    {
        using var document = JsonDocument.Parse("{\"count\":3}");
        var workerEvent = new WorkerEvent(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            3,
            WorkerEventKind.Progress,
            DateTimeOffset.UtcNow,
            document.RootElement);

        Assert.AreEqual(3, workerEvent.Sequence);
        Assert.AreEqual(WorkerEventKind.Progress, workerEvent.Kind);
        Assert.AreEqual(3, workerEvent.Payload.GetProperty("count").GetInt32());
    }

    [TestMethod]
    public void WorkerEventRejectsEmptyIdAndNegativeSequence()
    {
        using var document = JsonDocument.Parse("{}");
        Assert.ThrowsExactly<ArgumentException>(() => new WorkerEvent(
            Guid.Empty,
            InstanceId.New(),
            WorkerSessionId.New(),
            0,
            WorkerEventKind.Started,
            DateTimeOffset.UtcNow,
            document.RootElement));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new WorkerEvent(
            Guid.NewGuid(),
            InstanceId.New(),
            WorkerSessionId.New(),
            -1,
            WorkerEventKind.Started,
            DateTimeOffset.UtcNow,
            document.RootElement));
    }
}

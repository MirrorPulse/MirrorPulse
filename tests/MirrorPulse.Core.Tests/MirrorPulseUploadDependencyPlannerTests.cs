using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseUploadDependencyPlannerTests
{
    [TestMethod]
    public void PlanFollowsCfSharpJournalSequenceAcrossCreateMoveAndDelete()
    {
        var instance = InstanceId.New();
        var created = Command(1, instance, MirrorPulseWorkerChangeKind.Create);
        var moved = Command(2, instance, MirrorPulseWorkerChangeKind.Move);
        var deleted = Command(3, instance, MirrorPulseWorkerChangeKind.Delete);

        IReadOnlyList<MirrorPulseWorkerChangeCommand> ordered = MirrorPulseUploadDependencyPlanner.Plan(
            [deleted, created, moved]);

        CollectionAssert.AreEqual(new[] { created, moved, deleted }, ordered.ToArray());
    }

    [TestMethod]
    public void PlanRejectsRepeatedJournalIdentityOrSequence()
    {
        var instance = InstanceId.New();
        var first = Command(1, instance, MirrorPulseWorkerChangeKind.Create);
        var repeatedSequence = Command(1, instance, MirrorPulseWorkerChangeKind.Delete);
        var repeatedId = first with { Sequence = 2 };

        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseUploadDependencyPlanner.Plan([first, repeatedSequence]));
        Assert.ThrowsExactly<InvalidDataException>(() => MirrorPulseUploadDependencyPlanner.Plan([first, repeatedId]));
    }

    private static MirrorPulseWorkerChangeCommand Command(
        long sequence,
        InstanceId instanceId,
        MirrorPulseWorkerChangeKind kind) => new(
            Guid.NewGuid(), sequence, instanceId, "docs", kind, "a.txt", null, null,
            false, null, DateTimeOffset.UtcNow);
}

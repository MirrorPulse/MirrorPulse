using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseUploadDependencyPlannerTests
{
    [TestMethod]
    public void PlannerPlacesDependenciesBeforeDependentsDeterministically()
    {
        var firstId = Guid.NewGuid();
        var first = Create(firstId, DateTimeOffset.UtcNow);
        var second = Create(Guid.NewGuid(), first.CreatedAt.AddMinutes(1), [firstId]);
        var third = Create(Guid.NewGuid(), first.CreatedAt.AddMinutes(2));

        var ordered = MirrorPulseUploadDependencyPlanner.Plan([second, third, first]);

        CollectionAssert.AreEqual(new[] { first, second, third }, ordered.ToArray());
    }

    [TestMethod]
    public void PlannerRejectsMissingDependenciesAndCycles()
    {
        var missing = Guid.NewGuid();
        Assert.ThrowsExactly<InvalidDataException>(() =>
            MirrorPulseUploadDependencyPlanner.Plan([Create(Guid.NewGuid(), DateTimeOffset.UtcNow, [missing])]));

        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var first = Create(firstId, DateTimeOffset.UtcNow, [secondId]);
        var second = Create(secondId, DateTimeOffset.UtcNow.AddMinutes(1), [firstId]);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            MirrorPulseUploadDependencyPlanner.Plan([first, second]));
    }

    private static MirrorPulseQueuedUpload Create(
        Guid operationId,
        DateTimeOffset createdAt,
        IReadOnlyList<Guid>? dependencies = null) => new(
            operationId,
            InstanceId.New(),
            MirrorPulseUploadOperationKind.Update,
            $"{operationId:D}.txt",
            null,
            new byte[] { 1 },
            createdAt,
            dependencies);
}

using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseMutationExecutorTests
{
    internal static MirrorPulseMutationIntent Intent() => new(Guid.NewGuid(), InstanceId.New(), "local",
        MirrorPulseWorkerChangeKind.ContentUpdate, "file.txt", null, false, "before", 4, new string('A', 64),
        MirrorPulseMutationOrigin.Journal);

    [TestMethod]
    public async Task LostWorkerReplySurvivesRestartWithoutAnotherMutation()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        MirrorPulseMutationIntent intent = Intent();
        int mutations = 0;
        try
        {
            await using (var catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
            {
                await Assert.ThrowsExactlyAsync<IOException>(() => new MirrorPulseMutationExecutor(catalog).ExecuteAsync(intent,
                    _ => { mutations++; throw new IOException("Reply lost after remote commit."); },
                    (_, _) => throw new AssertFailedException("An unknown result cannot be acknowledged."), default).AsTask());
            }
            await using var reopened = await MirrorPulseProductCatalog.OpenAsync(paths);
            MirrorPulseMutationRecord record = (await reopened.ReadMutationAsync(intent.OperationId))!;
            Assert.AreEqual(intent, record.Intent);
            Assert.AreEqual(MirrorPulseMutationState.Ambiguous, record.State);
            await Assert.ThrowsExactlyAsync<MirrorPulseMutationAmbiguousException>(() => new MirrorPulseMutationExecutor(reopened)
                .ExecuteAsync(intent, _ => { mutations++; return ValueTask.FromResult<string?>("after"); },
                    (_, _) => ValueTask.CompletedTask, default).AsTask());
            Assert.AreEqual(1, mutations);
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() => reopened.PrepareMutationAsync(intent with { RelativePath = "other.txt" }));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task FailedOfficialAcknowledgementRetainsConfirmedResult()
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        try
        {
            await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
            MirrorPulseMutationIntent intent = Intent();
            await Assert.ThrowsExactlyAsync<IOException>(() => new MirrorPulseMutationExecutor(catalog).ExecuteAsync(intent,
                _ => ValueTask.FromResult<string?>("accepted"), (_, _) => throw new IOException("Ack failed."), default).AsTask());
            MirrorPulseMutationRecord record = (await catalog.ReadMutationAsync(intent.OperationId))!;
            Assert.AreEqual(MirrorPulseMutationState.RemoteAccepted, record.State);
            Assert.AreEqual("accepted", record.AcceptedRevision);
        }
        finally { Directory.Delete(root, true); }
    }
}

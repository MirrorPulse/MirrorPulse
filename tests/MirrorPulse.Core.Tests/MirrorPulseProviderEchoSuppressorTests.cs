using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseProviderEchoSuppressorTests
{
    [TestMethod]
    public async Task SuppressForwardsOperationAndObservationBudget()
    {
        var sink = new RecordingSink();
        var suppressor = new MirrorPulseProviderEchoSuppressor(sink);
        var expiry = DateTimeOffset.UtcNow.AddMinutes(5);

        await suppressor.SuppressAsync(
            CloudStateOperationKind.ContentUpdate,
            "docs/report.txt",
            expiry,
            remainingObservations: 2);

        Assert.AreEqual(CloudStateOperationKind.ContentUpdate, sink.Operation);
        Assert.AreEqual("docs/report.txt", sink.RelativePath);
        Assert.AreEqual(2, sink.RemainingObservations);
        Assert.AreEqual(expiry, sink.ExpiresAt);
    }

    [TestMethod]
    public async Task SuppressRejectsExpiredEntriesBeforeSink()
    {
        var sink = new RecordingSink();
        var suppressor = new MirrorPulseProviderEchoSuppressor(sink);

        await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() =>
            suppressor.SuppressAsync(
                CloudStateOperationKind.Delete,
                "docs/report.txt",
                DateTimeOffset.UtcNow.AddMinutes(-1)).AsTask());
        Assert.IsNull(sink.Operation);
    }

    private sealed class RecordingSink : IMirrorPulseProviderEchoSink
    {
        public CloudStateOperationKind? Operation { get; private set; }
        public string? RelativePath { get; private set; }
        public DateTimeOffset ExpiresAt { get; private set; }
        public int RemainingObservations { get; private set; }

        public ValueTask SuppressAsync(
            CloudStateOperationKind operation,
            string relativePath,
            DateTimeOffset expiresAt,
            Guid? itemId,
            string? previousRelativePath,
            int remainingObservations,
            CancellationToken cancellationToken)
        {
            Operation = operation;
            RelativePath = relativePath;
            ExpiresAt = expiresAt;
            RemainingObservations = remainingObservations;
            return ValueTask.CompletedTask;
        }
    }
}

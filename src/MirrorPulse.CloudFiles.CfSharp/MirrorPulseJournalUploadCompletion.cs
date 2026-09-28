using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>Records Worker outcomes through CfSharp's public journal and revision transactions.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseJournalUploadCompletion
{
    private readonly CloudLocalChangeFeed _feed;
    private readonly MirrorPulseCfSharpStateSession _state;
    private readonly MirrorPulseUploadRetryScheduler _retry;

    public MirrorPulseJournalUploadCompletion(
        CloudLocalChangeFeed feed,
        MirrorPulseCfSharpStateSession state,
        BackoffPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(policy);
        _feed = feed;
        _state = state;
        _retry = new MirrorPulseUploadRetryScheduler(policy);
    }

    public ValueTask AcknowledgeSuccessfulUploadAsync(
        Guid operationId,
        string? remoteRevision,
        CancellationToken cancellationToken = default) =>
        _feed.AcknowledgeAsync(
            [new CloudLocalChangeAcknowledgement(operationId, remoteRevision)],
            cancellationToken);

    public async ValueTask<RetryAfterDirective> DeferFailedUploadAsync(
        Guid operationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using ICloudStateTransaction transaction = await _state.OpenStore
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        CloudOperationJournalEntry existing = await transaction.Operations
            .GetAsync(operationId, cancellationToken).ConfigureAwait(false)
            ?? throw new FileNotFoundException("The CfSharp local operation is no longer pending.");
        RetryAfterDirective directive = _retry.Schedule(existing.AttemptCount, now);
        await transaction.Operations.UpdateAsync(
            new CloudOperationJournalEntry(
                existing.OperationId,
                existing.Kind,
                existing.ItemId,
                existing.Payload.Span,
                existing.CreatedAt,
                directive.Attempt,
                directive.RetryAt,
                existing.Sequence),
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return directive;
    }

    public async ValueTask<DateTimeOffset?> GetRetryAfterAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await using ICloudStateTransaction transaction = await _state.OpenStore
            .BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        CloudOperationJournalEntry? entry = await transaction.Operations
            .GetAsync(operationId, cancellationToken).ConfigureAwait(false);
        await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
        return entry?.RetryAfter;
    }
}

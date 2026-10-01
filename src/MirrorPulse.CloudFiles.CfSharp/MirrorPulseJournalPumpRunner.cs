using System.Collections.Concurrent;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseJournalPumpHealth(bool Healthy, int PendingFaults, string? LastErrorCode);

/// <summary>Isolates source, dispatch and reporting failures without acknowledging failed commands.</summary>
public sealed class MirrorPulseJournalPumpRunner
{
    private readonly ConcurrentDictionary<Guid, string> _faults = new();
    public MirrorPulseJournalPumpHealth Health => new(_faults.IsEmpty, _faults.Count,
        _faults.Values.Order(StringComparer.Ordinal).FirstOrDefault());

    public async ValueTask<bool> RunCycleAsync(
        Func<CancellationToken, ValueTask<MirrorPulseJournalUploadBatch>> read,
        Func<MirrorPulseWorkerChangeCommand, CancellationToken, ValueTask<bool>> dispatch,
        Func<MirrorPulseWorkerChangeCommand?, string, Exception, CancellationToken, ValueTask> report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(dispatch);
        ArgumentNullException.ThrowIfNull(report);
        MirrorPulseJournalUploadBatch batch;
        try
        {
            batch = await read(cancellationToken).ConfigureAwait(false);
            _faults.TryRemove(Guid.Empty, out _);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await ReportAsync(null, "JournalReadFailed", exception, report, cancellationToken).ConfigureAwait(false);
            return false;
        }
        if (batch.RequiresFullRescan) return false;
        bool progressed = false;
        foreach (MirrorPulseWorkerChangeCommand command in batch.ReadyCommands)
        {
            try
            {
                bool accepted = await dispatch(command, cancellationToken).ConfigureAwait(false);
                progressed |= accepted;
                if (accepted) _faults.TryRemove(command.OperationId, out _);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await ReportAsync(command, exception is MirrorPulseJournalAcknowledgementException
                    ? "JournalAcknowledgementFailed" : exception is MirrorPulseMutationAmbiguousException ? "MutationOutcomeAmbiguous" :
                    "JournalCommandFailed", exception, report, cancellationToken).ConfigureAwait(false);
            }
        }
        return progressed;
    }

    private async ValueTask ReportAsync(MirrorPulseWorkerChangeCommand? command, string code, Exception exception,
        Func<MirrorPulseWorkerChangeCommand?, string, Exception, CancellationToken, ValueTask> report,
        CancellationToken cancellationToken)
    {
        _faults[command?.OperationId ?? Guid.Empty] = code;
        try { await report(command, code, exception, cancellationToken).ConfigureAwait(false); }
        catch (Exception reportingFailure) when (reportingFailure is not OperationCanceledException)
        {
            // The in-memory fault remains observable even while the product catalog is unavailable.
        }
    }
}

public sealed class MirrorPulseJournalAcknowledgementException : IOException
{
    public MirrorPulseJournalAcknowledgementException() { }
    public MirrorPulseJournalAcknowledgementException(string? message) : base(message) { }
    public MirrorPulseJournalAcknowledgementException(string? message, Exception? innerException) : base(message, innerException) { }
}

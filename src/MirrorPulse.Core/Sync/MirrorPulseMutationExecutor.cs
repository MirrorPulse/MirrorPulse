using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.Sync;

/// <summary>Persists remote execution intent before dispatch; uncertainty never authorizes another mutation.</summary>
public sealed class MirrorPulseMutationExecutor(MirrorPulseProductCatalog catalog)
{
    private readonly MirrorPulseProductCatalog _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public async ValueTask ExecuteAsync(MirrorPulseMutationIntent intent,
        Func<CancellationToken, ValueTask<string?>> mutate,
        Func<string?, CancellationToken, ValueTask> acknowledge, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        ArgumentNullException.ThrowIfNull(acknowledge);
        MirrorPulseMutationRecord record = await _catalog.PrepareMutationAsync(intent, cancellationToken).ConfigureAwait(false);
        if (record.State == MirrorPulseMutationState.Acknowledged) return;
        if (record.State != MirrorPulseMutationState.Prepared) throw new MirrorPulseMutationAmbiguousException();
        await _catalog.TransitionMutationAsync(intent.OperationId, record.State, MirrorPulseMutationState.Executing,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        string? revision;
        try { revision = await mutate(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await _catalog.TransitionMutationAsync(intent.OperationId, MirrorPulseMutationState.Executing,
                exception is MirrorPulseWorkerMutationConflictException ? MirrorPulseMutationState.Conflict : MirrorPulseMutationState.Ambiguous,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            throw;
        }
        await _catalog.TransitionMutationAsync(intent.OperationId, MirrorPulseMutationState.Executing,
            MirrorPulseMutationState.RemoteAccepted, revision, cancellationToken).ConfigureAwait(false);
        await acknowledge(revision, cancellationToken).ConfigureAwait(false);
        await _catalog.TransitionMutationAsync(intent.OperationId, MirrorPulseMutationState.RemoteAccepted,
            MirrorPulseMutationState.Acknowledged, revision, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class MirrorPulseMutationAmbiguousException : IOException
{
    public MirrorPulseMutationAmbiguousException() : base("The previous remote outcome requires reconciliation before retry.") { }
    public MirrorPulseMutationAmbiguousException(string? message) : base(message) { }
    public MirrorPulseMutationAmbiguousException(string? message, Exception? innerException) : base(message, innerException) { }
}

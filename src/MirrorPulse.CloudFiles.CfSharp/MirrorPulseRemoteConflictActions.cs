using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseRemoteConflictActionOutcome(
    bool Resolved,
    bool CommandQueued,
    string? PreservedPath,
    CloudRemoteApplyEntryStatus? CfSharpStatus);

/// <summary>Executes product conflict choices through CfSharp's public resolution API.</summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseRemoteConflictActions
{
    private readonly Func<Guid, CloudRemoteConflictDecision, CancellationToken,
        ValueTask<CloudRemoteApplyEntryStatus>> _resolve;
    private readonly MirrorPulseConflictCopyStore _copies;
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly MirrorPulseConflictCenter _center;

    public MirrorPulseRemoteConflictActions(
        Func<Guid, CloudRemoteConflictDecision, CancellationToken,
            ValueTask<CloudRemoteApplyEntryStatus>> resolve,
        MirrorPulseConflictCopyStore copies,
        MirrorPulseProductCatalog catalog,
        MirrorPulseConflictCenter center)
    {
        _resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        _copies = copies ?? throw new ArgumentNullException(nameof(copies));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _center = center ?? throw new ArgumentNullException(nameof(center));
    }

    public static MirrorPulseRemoteConflictActions For(
        CloudFileSystem fileSystem,
        MirrorPulseConflictCopyStore copies,
        MirrorPulseProductCatalog catalog,
        MirrorPulseConflictCenter center)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return new(async (id, decision, cancellationToken) =>
            (await fileSystem.ResolveRemoteConflictAsync(
                id, new CloudRemoteConflictResolution(decision), cancellationToken)
                .ConfigureAwait(false)).Status,
            copies, catalog, center);
    }

    public async Task<MirrorPulseRemoteConflictActionOutcome> ApplyAsync(
        Guid commandId,
        MirrorPulseConflictRecord conflict,
        MirrorPulseConflictAction action,
        MirrorPulseConflictPreservedSide? preservedSide = null,
        Func<CancellationToken, ValueTask<Stream>>? openRemote = null,
        CancellationToken cancellationToken = default)
    {
        if (commandId == Guid.Empty)
        {
            throw new ArgumentException("The user command ID cannot be empty.", nameof(commandId));
        }

        ArgumentNullException.ThrowIfNull(conflict);
        if (conflict.Source != MirrorPulseConflictSource.CfSharpRemote || !conflict.IsPending)
        {
            throw new ArgumentException("This action requires a pending CfSharp remote conflict.", nameof(conflict));
        }

        if (action == MirrorPulseConflictAction.KeepLocal)
        {
            throw new NotSupportedException(
                "CfSharp preview keeps KeepLocal unresolved; MP cannot report it as a completed choice.");
        }

        if (action == MirrorPulseConflictAction.KeepBoth && preservedSide is null)
        {
            throw new ArgumentException("KeepBoth requires the side to preserve.", nameof(preservedSide));
        }

        if (action != MirrorPulseConflictAction.KeepBoth && preservedSide is not null)
        {
            throw new ArgumentException("Only KeepBoth may select a preserved side.", nameof(preservedSide));
        }

        if (action == MirrorPulseConflictAction.KeepBoth &&
            preservedSide == MirrorPulseConflictPreservedSide.Remote && openRemote is null)
        {
            throw new ArgumentException(
                "Preserving the remote side requires an Adapter stream.", nameof(openRemote));
        }

        string actionName = action == MirrorPulseConflictAction.KeepBoth
            ? $"keep-both-{preservedSide!.Value.ToString().ToLowerInvariant()}"
            : action.ToString().ToLowerInvariant();
        await _catalog.SaveUserCommandAsync(new MirrorPulseUserCommandRecord(
            commandId, actionName, conflict.ConflictId.ToString("D"), "pending"), cancellationToken)
            .ConfigureAwait(false);

        if (action is MirrorPulseConflictAction.Retry or
            MirrorPulseConflictAction.DeleteLocal or MirrorPulseConflictAction.DeleteRemote)
        {
            // These are product/Worker commands. CfSharp exposes no equivalent terminal decision.
            return new(false, true, null, null);
        }

        string? preservedPath = null;
        if (action == MirrorPulseConflictAction.KeepBoth)
        {
            Func<CancellationToken, ValueTask<Stream>> source = preservedSide == MirrorPulseConflictPreservedSide.Local
                ? token => _copies.OpenLocalAsync(conflict, token)
                : openRemote!;
            preservedPath = await _copies.PreserveAsync(
                conflict, preservedSide!.Value, source, cancellationToken).ConfigureAwait(false);
        }

        CloudRemoteConflictDecision decision = action == MirrorPulseConflictAction.Defer
            ? CloudRemoteConflictDecision.Defer
            : CloudRemoteConflictDecision.KeepRemote;
        CloudRemoteApplyEntryStatus status;
        try
        {
            status = await _resolve(conflict.ConflictId, decision, cancellationToken).ConfigureAwait(false);
        }
        catch (KeyNotFoundException) when (action != MirrorPulseConflictAction.Defer)
        {
            // CfSharp removes the conflict only after a successful apply. A retry converges here.
            status = CloudRemoteApplyEntryStatus.AlreadyApplied;
        }

        bool resolved = action != MirrorPulseConflictAction.Defer &&
            status is CloudRemoteApplyEntryStatus.Applied or CloudRemoteApplyEntryStatus.AlreadyApplied;
        if (resolved)
        {
            _center.Remove(conflict.ConflictId);
            await _catalog.SaveUserCommandAsync(new MirrorPulseUserCommandRecord(
                commandId, actionName, conflict.ConflictId.ToString("D"), "resolved"), cancellationToken)
                .ConfigureAwait(false);
        }

        return new(resolved, false, preservedPath, status);
    }
}

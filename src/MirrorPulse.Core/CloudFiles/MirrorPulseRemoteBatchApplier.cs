using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public sealed record MirrorPulseRemoteBatchOutcome(
    CloudRemoteBatchStatus Status,
    ReadOnlyMemory<byte> SafeCursor,
    bool RequiresRetry,
    int AppliedEntryCount,
    IReadOnlyList<Guid> ConflictIds);

/// <summary>
/// Applies one ordered remote change batch and exposes only the durable result needed by MP.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseRemoteBatchApplier
{
    private readonly Func<
        CloudRemoteChangeBatch,
        CloudRemoteApplyOptions,
        CancellationToken,
        ValueTask<MirrorPulseRemoteBatchOutcome>> _apply;

    public MirrorPulseRemoteBatchApplier(
        Func<
            CloudRemoteChangeBatch,
            CloudRemoteApplyOptions,
            CancellationToken,
            ValueTask<MirrorPulseRemoteBatchOutcome>> apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        _apply = apply;
    }

    public static MirrorPulseRemoteBatchApplier For(CloudFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return new(async (batch, options, cancellationToken) =>
        {
            var result = await fileSystem
                .ApplyRemoteChangesAsync(batch, options, cancellationToken)
                .ConfigureAwait(false);
            return new(
                result.Status,
                result.SafeCursor,
                result.RequiresRetry,
                result.AppliedEntryCount,
                result.ConflictIds);
        });
    }

    public ValueTask<MirrorPulseRemoteBatchOutcome> ApplyAsync(
        CloudRemoteChangeBatch batch,
        CloudRemoteApplyOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        return _apply(batch, options ?? CloudRemoteApplyOptions.Default, cancellationToken);
    }
}

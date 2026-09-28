namespace MirrorPulse.Core.CloudFiles;

public interface IMirrorPulseCrashRecoveryRuntime
{
    ValueTask CloseAsync(CancellationToken cancellationToken);

    ValueTask ReopenAsync(CancellationToken cancellationToken);

    ValueTask<MirrorPulseRecoveryProbe> ProbeAsync(CancellationToken cancellationToken);
}

public sealed record MirrorPulseRecoveryProbe(
    bool StateReadable,
    bool LocalChangeFeedReady);

public sealed record MirrorPulseCrashRecoveryReport(
    bool Closed,
    bool Reopened,
    bool StateReadable,
    bool LocalChangeFeedReady);

/// <summary>
/// Deterministic harness for exercising close/reopen/state probes around CfSharp resources.
/// </summary>
public sealed class MirrorPulseCrashRecoveryHarness
{
    private readonly IMirrorPulseCrashRecoveryRuntime _runtime;

    public MirrorPulseCrashRecoveryHarness(IMirrorPulseCrashRecoveryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public async ValueTask<MirrorPulseCrashRecoveryReport> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await _runtime.CloseAsync(cancellationToken).ConfigureAwait(false);
        await _runtime.ReopenAsync(cancellationToken).ConfigureAwait(false);
        var probe = await _runtime.ProbeAsync(cancellationToken).ConfigureAwait(false);
        return new(true, true, probe.StateReadable, probe.LocalChangeFeedReady);
    }
}

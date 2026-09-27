using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public interface IAdapterRuntimeStopper
{
    ValueTask StopAsync(InstallId installId, CancellationToken cancellationToken = default);
}

public sealed record HostShutdownResult
{
    public HostShutdownResult(
        MirrorPulseLifecycleState state,
        IReadOnlyList<InstallId> stoppedInstallations,
        IReadOnlyDictionary<InstallId, string> failures)
    {
        State = state;
        StoppedInstallations = new ReadOnlyCollection<InstallId>(stoppedInstallations.ToArray());
        Failures = new ReadOnlyDictionary<InstallId, string>(new Dictionary<InstallId, string>(failures));
    }

    public MirrorPulseLifecycleState State { get; }

    public IReadOnlyList<InstallId> StoppedInstallations { get; }

    public IReadOnlyDictionary<InstallId, string> Failures { get; }
}

/// <summary>
/// Stops enabled Adapter runtimes in order and reports any graceful-shutdown failures.
/// </summary>
public sealed class HostShutdownCoordinator
{
    private readonly IAdapterRuntimeStopper _stopper;

    public HostShutdownCoordinator(IAdapterRuntimeStopper stopper)
    {
        ArgumentNullException.ThrowIfNull(stopper);
        _stopper = stopper;
        State = MirrorPulseLifecycleState.Running;
    }

    public MirrorPulseLifecycleState State { get; private set; }

    public async Task<HostShutdownResult> StopAsync(IEnumerable<InstallId> installations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installations);
        if (State is MirrorPulseLifecycleState.Stopping or MirrorPulseLifecycleState.Stopped)
        {
            throw new InvalidOperationException("The host shutdown coordinator has already stopped.");
        }

        State = MirrorPulseLifecycleState.Stopping;
        var stopped = new List<InstallId>();
        var failures = new Dictionary<InstallId, string>();
        foreach (var installId in installations.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _stopper.StopAsync(installId, cancellationToken).ConfigureAwait(false);
                stopped.Add(installId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures[installId] = exception.Message;
            }
        }

        State = failures.Count == 0 ? MirrorPulseLifecycleState.Stopped : MirrorPulseLifecycleState.Failed;
        return new HostShutdownResult(State, stopped, failures);
    }
}

using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public interface IAdapterRuntimeStarter
{
    ValueTask StartAsync(InstallId installId, CancellationToken cancellationToken = default);
}

public sealed record HostStartupResult
{
    public HostStartupResult(
        MirrorPulseLifecycleState state,
        IReadOnlyList<InstallId> startedInstallations,
        IReadOnlyDictionary<InstallId, string> failures)
    {
        State = state;
        StartedInstallations = new ReadOnlyCollection<InstallId>(startedInstallations.ToArray());
        Failures = new ReadOnlyDictionary<InstallId, string>(new Dictionary<InstallId, string>(failures));
    }

    public MirrorPulseLifecycleState State { get; }

    public IReadOnlyList<InstallId> StartedInstallations { get; }

    public IReadOnlyDictionary<InstallId, string> Failures { get; }
}

/// <summary>
/// Starts enabled Adapter runtimes and keeps one failure from blocking other installations.
/// </summary>
public sealed class HostStartupCoordinator
{
    private readonly IAdapterRuntimeStarter _starter;

    public HostStartupCoordinator(IAdapterRuntimeStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        _starter = starter;
        State = MirrorPulseLifecycleState.Created;
    }

    public MirrorPulseLifecycleState State { get; private set; }

    public async Task<HostStartupResult> StartAsync(IEnumerable<InstallId> enabledInstallations, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enabledInstallations);
        if (State is MirrorPulseLifecycleState.Starting or MirrorPulseLifecycleState.Running)
        {
            throw new InvalidOperationException("The host startup coordinator is already running.");
        }

        State = MirrorPulseLifecycleState.Starting;
        var started = new List<InstallId>();
        var failures = new Dictionary<InstallId, string>();
        foreach (var installId in enabledInstallations.Distinct())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await _starter.StartAsync(installId, cancellationToken).ConfigureAwait(false);
                started.Add(installId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failures[installId] = exception.Message;
            }
        }

        State = failures.Count == 0 ? MirrorPulseLifecycleState.Running : MirrorPulseLifecycleState.Degraded;
        return new HostStartupResult(State, started, failures);
    }
}

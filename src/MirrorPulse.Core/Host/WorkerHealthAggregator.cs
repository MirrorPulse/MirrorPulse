using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public sealed record WorkerHealthSnapshot(
    InstanceId InstanceId,
    WorkerSessionId WorkerSessionId,
    WorkerLifecycleState LifecycleState,
    HealthStatus Status,
    long Sequence,
    DateTimeOffset ObservedAt,
    string? Detail);

/// <summary>
/// Aggregates monotonic Worker health responses for Host-level status reporting.
/// </summary>
public sealed class WorkerHealthAggregator
{
    private readonly object _gate = new();
    private readonly Dictionary<InstanceId, WorkerHealthSnapshot> _snapshots = new();

    public HealthStatus OverallStatus
    {
        get
        {
            lock (_gate)
            {
                if (_snapshots.Values.Any(snapshot => snapshot.Status == HealthStatus.Unhealthy))
                {
                    return HealthStatus.Unhealthy;
                }

                return _snapshots.Values.Any(snapshot => snapshot.Status == HealthStatus.Degraded)
                    ? HealthStatus.Degraded
                    : HealthStatus.Healthy;
            }
        }
    }

    public bool Observe(HealthMessage health)
    {
        ArgumentNullException.ThrowIfNull(health);
        lock (_gate)
        {
            if (_snapshots.TryGetValue(health.InstanceId, out var previous) && health.Sequence <= previous.Sequence)
            {
                return false;
            }

            _snapshots[health.InstanceId] = new WorkerHealthSnapshot(
                health.InstanceId,
                health.WorkerSessionId,
                MapLifecycle(health.Status),
                health.Status,
                health.Sequence,
                health.ObservedAt,
                health.Detail);
            return true;
        }
    }

    public void MarkDisconnected(InstanceId instanceId, WorkerSessionId workerSessionId, DateTimeOffset observedAt, string? detail = null)
    {
        lock (_gate)
        {
            var sequence = _snapshots.TryGetValue(instanceId, out var previous) ? previous.Sequence : 0;
            _snapshots[instanceId] = new WorkerHealthSnapshot(
                instanceId,
                workerSessionId,
                WorkerLifecycleState.Failed,
                HealthStatus.Unhealthy,
                sequence,
                observedAt,
                detail ?? "Worker disconnected.");
        }
    }

    public IReadOnlyDictionary<InstanceId, WorkerHealthSnapshot> Snapshot()
    {
        lock (_gate)
        {
            return new ReadOnlyDictionary<InstanceId, WorkerHealthSnapshot>(new Dictionary<InstanceId, WorkerHealthSnapshot>(_snapshots));
        }
    }

    private static WorkerLifecycleState MapLifecycle(HealthStatus status) => status switch
    {
        HealthStatus.Healthy => WorkerLifecycleState.Healthy,
        HealthStatus.Degraded => WorkerLifecycleState.Degraded,
        HealthStatus.Unhealthy => WorkerLifecycleState.Failed,
        _ => WorkerLifecycleState.Failed,
    };
}

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Lifecycle state of the MirrorPulse host services.
/// </summary>
public enum MirrorPulseLifecycleState
{
    Created,
    Starting,
    Running,
    Degraded,
    Stopping,
    Stopped,
    Failed
}

/// <summary>
/// Installation and runtime state of an Adapter.
/// </summary>
public enum AdapterLifecycleState
{
    Discovered,
    Verified,
    Staged,
    Installed,
    Configured,
    Disabled,
    Enabled,
    Starting,
    Handshaking,
    Healthy,
    Degraded,
    Restarting,
    Stopping,
    Updating,
    Quarantined,
    RolledBack
}

/// <summary>
/// Runtime state of one Adapter Worker process.
/// </summary>
public enum WorkerLifecycleState
{
    Created,
    Starting,
    Handshaking,
    Healthy,
    Degraded,
    Restarting,
    Stopping,
    Stopped,
    Failed
}

namespace MirrorPulse.Core.Adapters.Smb;

public enum MirrorPulseSmbWorkerState
{
    Created,
    Running,
    Stopped,
}

public sealed record MirrorPulseSmbWorkerOptions
{
    public MirrorPulseSmbWorkerOptions(Guid instanceId, string networkPath)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("An SMB Worker requires an instance ID.", nameof(instanceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(networkPath);
        if (!networkPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("An SMB source must be a UNC path.", nameof(networkPath));
        }

        InstanceId = instanceId;
        NetworkPath = Path.GetFullPath(networkPath);
    }

    public Guid InstanceId { get; }

    public string NetworkPath { get; }
}

/// <summary>
/// Lifecycle shell for the SMB Adapter Worker.
/// </summary>
public sealed class MirrorPulseSmbWorker : IAsyncDisposable
{
    public MirrorPulseSmbWorker(MirrorPulseSmbWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    public MirrorPulseSmbWorkerOptions Options { get; }

    public MirrorPulseSmbWorkerState State { get; private set; }

    public void Start()
    {
        if (State != MirrorPulseSmbWorkerState.Created)
        {
            throw new InvalidOperationException("An SMB Worker can only start once.");
        }

        State = MirrorPulseSmbWorkerState.Running;
    }

    public ValueTask StopAsync()
    {
        if (State == MirrorPulseSmbWorkerState.Running)
        {
            State = MirrorPulseSmbWorkerState.Stopped;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => StopAsync();
}

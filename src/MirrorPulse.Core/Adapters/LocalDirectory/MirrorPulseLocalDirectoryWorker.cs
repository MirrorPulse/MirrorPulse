namespace MirrorPulse.Core.Adapters.LocalDirectory;

public enum MirrorPulseLocalDirectoryWorkerState
{
    Created,
    Running,
    Stopped,
}

public sealed record MirrorPulseLocalDirectoryWorkerOptions
{
    public MirrorPulseLocalDirectoryWorkerOptions(Guid instanceId, string sourceDirectory)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("A local directory Worker requires an instance ID.", nameof(instanceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        InstanceId = instanceId;
        SourceDirectory = Path.GetFullPath(sourceDirectory);
    }

    public Guid InstanceId { get; }

    public string SourceDirectory { get; }
}

/// <summary>
/// Lifecycle shell for the local-directory Adapter Worker.
/// </summary>
public sealed class MirrorPulseLocalDirectoryWorker : IAsyncDisposable
{
    public MirrorPulseLocalDirectoryWorker(MirrorPulseLocalDirectoryWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    public MirrorPulseLocalDirectoryWorkerOptions Options { get; }

    public MirrorPulseLocalDirectoryWorkerState State { get; private set; }

    public void Start()
    {
        if (State != MirrorPulseLocalDirectoryWorkerState.Created)
        {
            throw new InvalidOperationException("A local directory Worker can only start once.");
        }

        if (!Directory.Exists(Options.SourceDirectory))
        {
            throw new DirectoryNotFoundException(Options.SourceDirectory);
        }

        State = MirrorPulseLocalDirectoryWorkerState.Running;
    }

    public ValueTask StopAsync()
    {
        if (State == MirrorPulseLocalDirectoryWorkerState.Running)
        {
            State = MirrorPulseLocalDirectoryWorkerState.Stopped;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => StopAsync();
}

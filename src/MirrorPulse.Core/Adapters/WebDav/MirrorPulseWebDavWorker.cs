namespace MirrorPulse.Core.Adapters.WebDav;

public enum MirrorPulseWebDavWorkerState
{
    Created,
    Running,
    Stopped,
}

public sealed record MirrorPulseWebDavWorkerOptions
{
    public MirrorPulseWebDavWorkerOptions(Guid instanceId, Uri baseUri)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("A WebDAV Worker requires an instance ID.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(baseUri);
        if (!baseUri.IsAbsoluteUri ||
            (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("A WebDAV endpoint must be an absolute HTTP or HTTPS URI.", nameof(baseUri));
        }

        InstanceId = instanceId;
        BaseUri = baseUri;
    }

    public Guid InstanceId { get; }

    public Uri BaseUri { get; }
}

/// <summary>
/// Lifecycle shell for the WebDAV Adapter Worker.
/// </summary>
public sealed class MirrorPulseWebDavWorker : IAsyncDisposable
{
    public MirrorPulseWebDavWorker(MirrorPulseWebDavWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    public MirrorPulseWebDavWorkerOptions Options { get; }

    public MirrorPulseWebDavWorkerState State { get; private set; }

    public void Start()
    {
        if (State != MirrorPulseWebDavWorkerState.Created)
        {
            throw new InvalidOperationException("A WebDAV Worker can only start once.");
        }

        State = MirrorPulseWebDavWorkerState.Running;
    }

    public ValueTask StopAsync()
    {
        if (State == MirrorPulseWebDavWorkerState.Running)
        {
            State = MirrorPulseWebDavWorkerState.Stopped;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => StopAsync();
}

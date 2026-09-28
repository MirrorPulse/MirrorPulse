namespace MirrorPulse.Core.Adapters.Ftp;

public enum MirrorPulseFtpSecurityMode
{
    Plain,
    ExplicitTls,
    ImplicitTls,
}

public enum MirrorPulseFtpWorkerState
{
    Created,
    Running,
    Stopped,
}

public sealed record MirrorPulseFtpWorkerOptions
{
    public MirrorPulseFtpWorkerOptions(
        Guid instanceId,
        Uri serverUri,
        MirrorPulseFtpSecurityMode securityMode)
    {
        if (instanceId == Guid.Empty)
        {
            throw new ArgumentException("An FTP Worker requires an instance ID.", nameof(instanceId));
        }

        ArgumentNullException.ThrowIfNull(serverUri);
        if (!serverUri.IsAbsoluteUri || serverUri.Scheme != Uri.UriSchemeFtp ||
            string.IsNullOrWhiteSpace(serverUri.Host) ||
            !string.IsNullOrEmpty(serverUri.UserInfo))
        {
            throw new ArgumentException("An FTP endpoint must be an absolute ftp:// URI without embedded credentials.", nameof(serverUri));
        }

        if (!Enum.IsDefined(securityMode))
        {
            throw new ArgumentOutOfRangeException(nameof(securityMode));
        }

        InstanceId = instanceId;
        ServerUri = serverUri;
        SecurityMode = securityMode;
    }

    public Guid InstanceId { get; }

    public Uri ServerUri { get; }

    public MirrorPulseFtpSecurityMode SecurityMode { get; }
}

/// <summary>
/// Lifecycle shell for the FTP and FTPS Adapter Worker.
/// </summary>
public sealed class MirrorPulseFtpWorker : IAsyncDisposable
{
    public MirrorPulseFtpWorker(MirrorPulseFtpWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
    }

    public MirrorPulseFtpWorkerOptions Options { get; }

    public MirrorPulseFtpWorkerState State { get; private set; }

    public void Start()
    {
        if (State != MirrorPulseFtpWorkerState.Created)
        {
            throw new InvalidOperationException("An FTP Worker can only start once.");
        }

        State = MirrorPulseFtpWorkerState.Running;
    }

    public ValueTask StopAsync()
    {
        if (State == MirrorPulseFtpWorkerState.Running)
        {
            State = MirrorPulseFtpWorkerState.Stopped;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => StopAsync();
}

using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public interface IMirrorPulseLocalChangeFeedRuntime
{
    bool IsStarted { get; }

    ValueTask StartAsync(CancellationToken cancellationToken);

    ValueTask<CloudLocalChangeBatch> ReadBatchAsync(CancellationToken cancellationToken);

    ValueTask DisposeAsync();
}

/// <summary>
/// Owns startup and shutdown of one CfSharp local change feed.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseLocalChangeFeedSession : IAsyncDisposable
{
    private readonly IMirrorPulseLocalChangeFeedRuntime _runtime;

    public MirrorPulseLocalChangeFeedSession(IMirrorPulseLocalChangeFeedRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        _runtime = runtime;
    }

    public static MirrorPulseLocalChangeFeedSession For(
        CloudFileSystem fileSystem,
        CloudLocalChangeFeedOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        return new(new NativeRuntime(fileSystem.CreateLocalChangeFeed(options ?? CloudLocalChangeFeedOptions.Default)));
    }

    public bool IsStarted => _runtime.IsStarted;

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted)
        {
            await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask<CloudLocalChangeBatch> ReadBatchAsync(CancellationToken cancellationToken = default)
    {
        if (!IsStarted)
        {
            throw new InvalidOperationException("The local change feed must be started before reading a batch.");
        }

        return _runtime.ReadBatchAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _runtime.DisposeAsync();

    private sealed class NativeRuntime : IMirrorPulseLocalChangeFeedRuntime
    {
        private readonly CloudLocalChangeFeed _feed;

        public NativeRuntime(CloudLocalChangeFeed feed) => _feed = feed;

        public bool IsStarted => _feed.IsStarted;

        public ValueTask StartAsync(CancellationToken cancellationToken) => _feed.StartAsync(cancellationToken);

        public ValueTask<CloudLocalChangeBatch> ReadBatchAsync(CancellationToken cancellationToken) =>
            _feed.ReadBatchAsync(cancellationToken);

        public ValueTask DisposeAsync() => _feed.DisposeAsync();
    }
}

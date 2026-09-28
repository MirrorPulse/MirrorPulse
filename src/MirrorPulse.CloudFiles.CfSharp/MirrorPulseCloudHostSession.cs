using System.Runtime.Versioning;
using System.Text;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp;

public interface IMirrorPulseCloudRuntime : IAsyncDisposable
{
    ValueTask StartAsync(CancellationToken cancellationToken);
}

public interface IMirrorPulseCloudRuntimeFactory
{
    IMirrorPulseCloudRuntime Create(MirrorPulseStoragePaths paths);
}

[SupportedOSPlatform("windows10.0.16299")]
public sealed class CfSharpMirrorPulseCloudRuntimeFactory : IMirrorPulseCloudRuntimeFactory
{
    private readonly ICloudDemandProvider? _provider;

    public CfSharpMirrorPulseCloudRuntimeFactory(ICloudDemandProvider? provider = null)
    {
        _provider = provider;
    }

    public IMirrorPulseCloudRuntime Create(MirrorPulseStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new CfSharpRuntime(new MirrorPulseCloudFileSystemBuilder(paths)
            .WithContentProvider(_provider ?? MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath))
            .Build());
    }

    private sealed class CfSharpRuntime(CloudFileSystem fileSystem) : IMirrorPulseCloudRuntime
    {
        public ValueTask StartAsync(CancellationToken cancellationToken) => fileSystem.StartAsync(cancellationToken);

        public ValueTask DisposeAsync() => fileSystem.DisposeAsync();
    }
}

/// <summary>
/// Owns one current-user Cloud Files process session. Persistent registration and SQLite state
/// survive ordinary shutdown; only explicit account removal unregisters the root.
/// </summary>
[SupportedOSPlatform("windows10.0.19041")]
public sealed class MirrorPulseCloudHostSession : IAsyncDisposable
{
    private static readonly Guid ProviderId = new("d194eacd-c38c-49df-a238-49871f9c8d1b");
    private static readonly byte[] RootIdentity = Encoding.UTF8.GetBytes("MirrorPulse/cloud-files/v1");

    private readonly MirrorPulseStoragePaths _paths;
    private readonly MirrorPulseSyncRootRegistrationCoordinator _registration;
    private readonly IMirrorPulseCloudRuntimeFactory _runtimeFactory;
    private readonly MirrorPulseSyncRootOwner _owner;
    private readonly MirrorPulseSyncRootDefinition _definition;
    private readonly MirrorPulseShellRegistrationProfile _profile;
    private IMirrorPulseCloudRuntime? _runtime;
    private bool _started;
    private bool _disposed;

    public MirrorPulseCloudHostSession(
        MirrorPulseStoragePaths paths,
        MirrorPulseSyncRootRegistrationCoordinator registration,
        IMirrorPulseCloudRuntimeFactory runtimeFactory,
        MirrorPulseSyncRootOwner owner,
        string currentUserSid,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(runtimeFactory);
        ArgumentNullException.ThrowIfNull(owner);
        _paths = paths;
        _registration = registration;
        _runtimeFactory = runtimeFactory;
        _owner = owner;
        _definition = new MirrorPulseSyncRootDefinition(paths.SyncRootPath, "0.1.0", ProviderId, RootIdentity);
        _profile = MirrorPulseShellSyncRootRegistrar.CreateProfile(_definition, currentUserSid, displayName);
    }

    public MirrorPulseSyncRootDefinition Definition => _definition;

    public MirrorPulseShellRegistrationProfile ShellProfile => _profile;

    public static MirrorPulseCloudHostSession CreateDefault(MirrorPulseStoragePaths paths, string displayName)
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("The current Windows user has no SID.");
        return new(
            paths,
            MirrorPulseSyncRootRegistrationCoordinator.CreateDefault(),
            new CfSharpMirrorPulseCloudRuntimeFactory(),
            MirrorPulseSyncRootOwner.CreateDefault(),
            sid,
            displayName);
    }

    public async ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            throw new InvalidOperationException("The Cloud Files Host session has already started.");
        }

        if (!_owner.TryAcquire(TimeSpan.Zero))
        {
            throw new InvalidOperationException("Another MirrorPulse Host owns this user's Cloud Files root.");
        }

        try
        {
            await _registration.EnsureRegisteredAsync(_definition, _profile, cancellationToken).ConfigureAwait(false);
            _runtime = _runtimeFactory.Create(_paths);
            await _runtime.StartAsync(cancellationToken).ConfigureAwait(false);
            _started = true;
        }
        catch (Exception startFailure)
        {
            try
            {
                if (_runtime is not null)
                {
                    await _runtime.DisposeAsync().ConfigureAwait(false);
                }

                _runtime = null;
                _owner.Dispose();
                _disposed = true;
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Cloud Files startup and resource cleanup both failed.",
                    startFailure,
                    cleanupFailure);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (_runtime is not null)
        {
            await _runtime.DisposeAsync().ConfigureAwait(false);
            _runtime = null;
        }

        _owner.Dispose();
        _disposed = true;
    }
}

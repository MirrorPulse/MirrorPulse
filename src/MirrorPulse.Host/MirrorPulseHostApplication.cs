using System.Text.Json;
using CfSharp;
using MirrorPulse.Adapter.Sdk;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Control.Compatibility;
using MirrorPulse.Control.Dispatch;
using MirrorPulse.Control.Transport;
using MirrorPulse.Core;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Packaging;
using MirrorPulse.Core.Security;
using MirrorPulse.Core.State;

namespace MirrorPulse.Host;

/// <summary>
/// Owns the complete non-UI Host composition for one current user.
/// </summary>
public sealed class MirrorPulseHostApplication : IAsyncDisposable
{
    private readonly MirrorPulseStoragePaths _paths;
    private readonly MirrorPulseHostLease _hostLease;
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly MirrorPulseConflictCenter _conflictCenter;
    private readonly MirrorPulseSystemNotificationPublisher _systemNotifications;
    private readonly AdapterInstanceProcessSupervisor _workers;
    private readonly MirrorPulseCloudHostSession _session;
    private readonly MirrorPulseActiveRemotePoller _remotePoller;
    private readonly MirrorPulseAdapterTopology _topology;
    private readonly CancellationTokenSource _shutdown = new();
    private MirrorPulseAppStatusPipe? _statusPipe;
    private MirrorPulseControlPipeServer? _controlPipe;
    private Task? _serveTask;
    private MirrorPulseLifecycleState _state = MirrorPulseLifecycleState.Created;
    private bool _disposed;

    private MirrorPulseHostApplication(
        MirrorPulseStoragePaths paths,
        MirrorPulseHostLease hostLease,
        MirrorPulseProductCatalog catalog,
        MirrorPulseConflictCenter conflictCenter,
        MirrorPulseSystemNotificationPublisher systemNotifications,
        AdapterInstanceProcessSupervisor workers,
        MirrorPulseCloudHostSession session,
        MirrorPulseActiveRemotePoller remotePoller,
        MirrorPulseAdapterTopology topology)
    {
        _paths = paths;
        _hostLease = hostLease;
        _catalog = catalog;
        _conflictCenter = conflictCenter;
        _systemNotifications = systemNotifications;
        _workers = workers;
        _session = session;
        _remotePoller = remotePoller;
        _topology = topology;
    }

    public MirrorPulseStoragePaths Paths => _paths;

    public MirrorPulseLifecycleState State => _state;

    public bool IsRunning => _state is MirrorPulseLifecycleState.Running or MirrorPulseLifecycleState.Degraded;

    public static async Task<MirrorPulseHostApplication> CreateAsync(
        MirrorPulseStoragePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var hostLease = MirrorPulseHostLease.CreateDefault();
        if (!hostLease.TryAcquire(TimeSpan.Zero))
        {
            hostLease.Dispose();
            throw new InvalidOperationException("Another MirrorPulse Host owns this current user's Host lease.");
        }

        MirrorPulseProductCatalog? catalog = null;
        MirrorPulseSystemNotificationPublisher? systemNotifications = null;
        AdapterInstanceProcessSupervisor? workers = null;
        MirrorPulseCloudHostSession? session = null;
        MirrorPulseActiveRemotePoller? remotePoller = null;
        try
        {
            _ = await new MirrorPulseConfigurationStore(
                Path.Combine(paths.DataRootPath, "config.json")).LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            catalog = await MirrorPulseProductCatalog.OpenAsync(paths, cancellationToken)
                .ConfigureAwait(false);
            MirrorPulseAdapterTopology topology = await catalog.ReadAdapterTopologyAsync(cancellationToken)
                .ConfigureAwait(false);
            var conflictCenter = new MirrorPulseConflictCenter();
            systemNotifications = new MirrorPulseSystemNotificationPublisher();
            var conflictNotifications = new MirrorPulseConflictNotificationBridge(
                systemNotifications.PublishAsync);
            var credentialStore = new WindowsCredentialManagerStore();
            MirrorPulseCloudHostSession? currentSession = null;
            async ValueTask ApplyCloudRemoteBatchAsync(
                InstanceId instanceId,
                CloudRemoteChangeBatch batch,
                CancellationToken batchCancellationToken)
            {
                CloudRemoteApplyResult result = await (currentSession ??
                    throw new InvalidOperationException("The Cloud Files session has not started."))
                    .ApplyRemoteBatchAsync(
                        instanceId, batch, catalog, conflictCenter, conflictNotifications,
                        cancellationToken: batchCancellationToken).ConfigureAwait(false);
                if (result.RequiresRetry)
                {
                    await catalog.SaveInstanceRuntimeStateAsync(
                        new(instanceId, "Remote retry", false, null, "RemoteBatchRetry"),
                        batchCancellationToken).ConfigureAwait(false);
                }
            }

            async ValueTask ApplyRemoteBatchAsync(
                InstanceId instanceId,
                JsonElement payload,
                CancellationToken batchCancellationToken)
            {
                AdapterRemoteChangeBatch adapterBatch = payload.Deserialize<AdapterRemoteChangeBatch>()
                    ?? throw new InvalidDataException("The Adapter remote batch payload is empty.");
                CloudRemoteChangeBatch batch = MirrorPulseAdapterRemoteBatchMapper.Map(
                    instanceId, topology.Roots, adapterBatch);
                await ApplyCloudRemoteBatchAsync(instanceId, batch, batchCancellationToken)
                    .ConfigureAwait(false);
            }

            workers = new AdapterInstanceProcessSupervisor(catalog, credentialStore, ApplyRemoteBatchAsync);
            var rootRouter = new MirrorPulseRootRouter(paths.SyncRootPath, topology.Roots);
            var directorySource = new MirrorPulseAdapterDirectoryPageSource(workers);
            var provider = new MirrorPulseDemandProvider(rootRouter, workers, directorySource);
            session = MirrorPulseCloudHostSession.CreateDefault(
                paths, topology.Instances, topology.Roots, provider, workers, workers,
                rootRouter, catalog, instanceId => topology.Instances.Any(instance =>
                    instance.InstanceId == instanceId && instance.Enabled),
                conflictCenter, conflictNotifications, workers);
            currentSession = session;
            var remoteSnapshotStore = new MirrorPulseFileRemotePollSnapshotStore(paths.DataRootPath);
            remotePoller = new MirrorPulseActiveRemotePoller(
                directorySource, topology.Instances, topology.Roots, ApplyCloudRemoteBatchAsync,
                snapshotStore: remoteSnapshotStore);

            var application = new MirrorPulseHostApplication(
                paths, hostLease, catalog, conflictCenter, systemNotifications,
                workers, session, remotePoller, topology);
            application.InitializeControlPlane(credentialStore);
            return application;
        }
        catch
        {
            if (remotePoller is not null)
            {
                await remotePoller.DisposeAsync().ConfigureAwait(false);
            }

            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }

            if (workers is not null)
            {
                await workers.DisposeAsync().ConfigureAwait(false);
            }

            if (catalog is not null)
            {
                await catalog.DisposeAsync().ConfigureAwait(false);
            }

            systemNotifications?.Dispose();
            hostLease.Dispose();
            throw;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_state is MirrorPulseLifecycleState.Starting or MirrorPulseLifecycleState.Running)
        {
            throw new InvalidOperationException("The MirrorPulse Host is already running.");
        }

        _hostLease.EnsureHeld();
        _state = MirrorPulseLifecycleState.Starting;
        try
        {
            await _session.StartAsync(cancellationToken).ConfigureAwait(false);
            await _workers.StartAsync(_topology).ConfigureAwait(false);
            await _remotePoller.StartAsync(cancellationToken).ConfigureAwait(false);
            _state = MirrorPulseLifecycleState.Running;
        }
        catch
        {
            _state = MirrorPulseLifecycleState.Failed;
            throw;
        }
    }

    public async Task ServeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsRunning)
        {
            throw new InvalidOperationException("The MirrorPulse Host must be running before serving control pipes.");
        }

        if (_serveTask is not null)
        {
            throw new InvalidOperationException("The MirrorPulse Host control pipes are already serving.");
        }

        if (_statusPipe is null || _controlPipe is null)
        {
            throw new InvalidOperationException("The MirrorPulse Host control plane is not initialized.");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdown.Token);
        _serveTask = Task.WhenAll(
            _statusPipe.ServeAsync(linked.Token),
            _controlPipe.ServeAsync(linked.Token));
        await _serveTask.ConfigureAwait(false);
    }

    public async Task<MirrorPulseAppStatusResponse> ReadStatusAsync(
        CancellationToken cancellationToken = default)
    {
        MirrorPulseAdapterTopology current = await _catalog.ReadAdapterTopologyAsync(cancellationToken)
            .ConfigureAwait(false);
        MirrorPulseCloudStatusSnapshot cloud = await _session.ReadStatusAsync(
            current.Instances.Select(instance => instance.InstanceId), cancellationToken)
            .ConfigureAwait(false);
        var entries = new List<MirrorPulseAppInstanceStatus>(current.Instances.Count);
        foreach (var instance in current.Instances)
        {
            MirrorPulseInstanceRuntimeState? runtime = await _catalog.ReadInstanceRuntimeStateAsync(
                instance.InstanceId, cancellationToken).ConfigureAwait(false);
            MirrorPulseInstanceCursorStatus? cursor = cloud.Cursors.SingleOrDefault(item =>
                item.InstanceId == instance.InstanceId);
            entries.Add(new MirrorPulseAppInstanceStatus(instance.InstanceId.ToString(),
                instance.DisplayName, instance.Enabled,
                instance.Enabled ? runtime?.Phase ?? "Not running" : "Offline",
                cursor?.CursorFingerprint, cursor?.UpdatedAt, runtime?.LastSuccessfulSync,
                runtime?.LastErrorCode, runtime?.TransferProgress));
        }

        IReadOnlySet<Guid> snoozed = await _catalog.ReadSnoozedConflictIdsAsync(cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<MirrorPulseConflictRecord> uploadConflicts =
            await _catalog.ReadUploadConflictsAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        var remoteNotifications = (await _catalog.ReadRemoteConflictProjectionsAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(conflict => cloud.PendingRemoteConflictIds.Contains(conflict.ConflictId))
            .Select(conflict => new MirrorPulseAppNotification(
                conflict.ConflictId.ToString("D"), conflict.RelativePath,
                conflict.DetectedAt, snoozed.Contains(conflict.ConflictId)));
        var notifications = remoteNotifications.Concat(uploadConflicts.Select(conflict =>
                new MirrorPulseAppNotification(conflict.ConflictId.ToString("D"),
                    conflict.RelativePath, conflict.DetectedAt,
                    snoozed.Contains(conflict.ConflictId), MirrorPulseConflictSource.Upload)))
            .OrderByDescending(item => item.DetectedAt)
            .ToArray();
        return new MirrorPulseAppStatusResponse(cloud.PendingUploadCount,
            cloud.PendingRemoteConflictCount, entries, notifications,
            PendingUploadConflicts: uploadConflicts.Count);
    }

    private void InitializeControlPlane(ISecureCredentialStore credentialStore)
    {
        var provisioner = new MirrorPulseAdapterInstanceProvisioner(
            _catalog, credentialStore, _paths.DataRootPath);
        _statusPipe = new MirrorPulseAppStatusPipe(
            ReadStatusAsync,
            SnoozeConflictAsync,
            SetInstanceEnabledAsync,
            SelectInstallationAsync,
            InstallAdapterAsync,
            (request, cancellationToken) => CreateInstanceAsync(provisioner, request, cancellationToken),
            ResolveConflictAsync);
        var dispatcher = new MirrorPulseControlDispatcher();
        new MirrorPulseLegacyCommandBridge(
            ReadStatusAsync,
            SnoozeConflictAsync,
            ResolveConflictAsync,
            SetInstanceEnabledAsync,
            SelectInstallationAsync,
            InstallAdapterAsync,
            (request, cancellationToken) => CreateInstanceAsync(provisioner, request, cancellationToken))
            .Register(dispatcher);
        _controlPipe = new MirrorPulseControlPipeServer(dispatcher.DispatchAsync);
    }

    private async Task<MirrorPulseAppStatusResponse> InstallAdapterAsync(
        string packagePath,
        CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(packagePath.Trim());
        if (!string.Equals(Path.GetExtension(source), ".mpadapter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The Host install command accepts only .mpadapter files.", nameof(packagePath));
        }

        string signaturePath = source + ".signature.json";
        string runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.X64 => "win-x64",
            System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
            _ => throw new PlatformNotSupportedException("The Adapter process architecture is unsupported."),
        };
        string installationRoot = Path.Combine(_paths.DataRootPath, "adapters", "installed");
        InstalledAdapter installed = await _catalog.InstallSignedAdapterAsync(
            source, signaturePath, installationRoot, runtimeIdentifier, cancellationToken)
            .ConfigureAwait(false);
        return (await ReadStatusAsync(cancellationToken).ConfigureAwait(false)) with
        {
            InstalledAdapterId = installed.InstallId.ToString(),
        };
    }

    private async Task<MirrorPulseAppStatusResponse> CreateInstanceAsync(
        MirrorPulseAdapterInstanceProvisioner provisioner,
        MirrorPulseCreateInstanceRequest request,
        CancellationToken cancellationToken)
    {
        AdapterInstance instance = await provisioner.CreateAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return (await ReadStatusAsync(cancellationToken).ConfigureAwait(false)) with
        {
            CreatedInstanceId = instance.InstanceId.ToString(),
        };
    }

    private async Task<MirrorPulseAppStatusResponse> SnoozeConflictAsync(
        Guid conflictId,
        CancellationToken cancellationToken)
    {
        MirrorPulseAppStatusResponse current = await ReadStatusAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!current.Notifications.Any(item => item.ConflictId == conflictId.ToString("D")))
        {
            throw new FileNotFoundException("The pending conflict notification was not found.");
        }

        await _catalog.SetConflictSnoozedAsync(conflictId, true, cancellationToken)
            .ConfigureAwait(false);
        return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MirrorPulseAppStatusResponse> SetInstanceEnabledAsync(
        InstanceId instanceId,
        bool enabled,
        CancellationToken cancellationToken)
    {
        await _catalog.SetInstanceEnabledAsync(instanceId, enabled, cancellationToken)
            .ConfigureAwait(false);
        return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MirrorPulseAppStatusResponse> SelectInstallationAsync(
        InstanceId instanceId,
        InstallId installId,
        CancellationToken cancellationToken)
    {
        await _catalog.SelectInstanceInstallationAsync(instanceId, installId, cancellationToken)
            .ConfigureAwait(false);
        return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<MirrorPulseAppStatusResponse> ResolveConflictAsync(
        Guid conflictId,
        MirrorPulseConflictAction action,
        CancellationToken cancellationToken)
    {
        if (await _catalog.ReadUploadConflictAsync(conflictId, cancellationToken)
                .ConfigureAwait(false) is not null)
        {
            await _session.ApplyUploadConflictAsync(conflictId, action, cancellationToken)
                .ConfigureAwait(false);
            _conflictCenter.Remove(conflictId);
        }
        else
        {
            MirrorPulseRemoteConflictActionOutcome outcome =
                await _session.ApplyRemoteConflictAsync(conflictId, action, _catalog,
                    cancellationToken).ConfigureAwait(false);
            if (outcome.Resolved)
            {
                _conflictCenter.Remove(conflictId);
            }
        }

        return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _state = MirrorPulseLifecycleState.Stopping;
        _shutdown.Cancel();
        if (_serveTask is not null)
        {
            try
            {
                await _serveTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _remotePoller.DisposeAsync().ConfigureAwait(false);
        await _session.DisposeAsync().ConfigureAwait(false);
        await _workers.DisposeAsync().ConfigureAwait(false);
        await _catalog.DisposeAsync().ConfigureAwait(false);
        _systemNotifications.Dispose();
        _shutdown.Dispose();
        _hostLease.Dispose();
        _state = MirrorPulseLifecycleState.Stopped;
        _disposed = true;
    }
}

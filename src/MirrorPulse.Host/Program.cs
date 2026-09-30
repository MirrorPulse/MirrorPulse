using System.Text.Json;
using CfSharp;
using MirrorPulse.Adapter.Sdk;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Packaging;
using MirrorPulse.Core.Security;
using MirrorPulse.Core.State;
using MirrorPulse.Host;

if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
{
    Console.Error.WriteLine("MirrorPulse requires Windows 10 version 2004 or later.");
    return 1;
}

if (args.Length > 1 || (args.Length == 1 && args[0] != "--run-once"))
{
    Console.Error.WriteLine("Usage: MirrorPulse.Host [--run-once]");
    return 2;
}

string dataRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    ProductInfo.Name);
string syncRoot = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ProductInfo.Name);
var paths = new MirrorPulseStoragePaths(syncRoot, dataRoot);

try
{
    _ = await new MirrorPulseConfigurationStore(
        Path.Combine(paths.DataRootPath, "config.json")).LoadAsync();
    await using var catalog = await MirrorPulseProductCatalog.OpenAsync(paths);
    MirrorPulseAdapterTopology topology = await catalog.ReadAdapterTopologyAsync();
    var conflictCenter = new MirrorPulseConflictCenter();
    using var systemNotifications = new MirrorPulseSystemNotificationPublisher();
    var conflictNotifications = new MirrorPulseConflictNotificationBridge(
        systemNotifications.PublishAsync);
    using var shutdown = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };
    Console.CancelKeyPress += cancel;
    try
    {
        MirrorPulseCloudHostSession? currentSession = null;
        async ValueTask ApplyCloudRemoteBatchAsync(
            InstanceId instanceId,
            CloudRemoteChangeBatch batch,
            CancellationToken cancellationToken)
        {
            CloudRemoteApplyResult result = await (currentSession ??
                throw new InvalidOperationException("The Cloud Files session has not started."))
                .ApplyRemoteBatchAsync(
                    instanceId, batch, catalog, conflictCenter, conflictNotifications,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            if (result.RequiresRetry)
            {
                await catalog.SaveInstanceRuntimeStateAsync(
                    new(instanceId, "Remote retry", false, null, "RemoteBatchRetry"), cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        async ValueTask ApplyRemoteBatchAsync(
            InstanceId instanceId,
            JsonElement payload,
            CancellationToken cancellationToken)
        {
            AdapterRemoteChangeBatch adapterBatch = payload.Deserialize<AdapterRemoteChangeBatch>()
                ?? throw new InvalidDataException("The Adapter remote batch payload is empty.");
            CloudRemoteChangeBatch batch = MirrorPulseAdapterRemoteBatchMapper.Map(
                instanceId, topology.Roots, adapterBatch);
            await ApplyCloudRemoteBatchAsync(instanceId, batch, cancellationToken).ConfigureAwait(false);
        }

        var credentialStore = new WindowsCredentialManagerStore();
        await using var workers = new AdapterInstanceProcessSupervisor(catalog,
            credentialStore, ApplyRemoteBatchAsync);
        var rootRouter = new MirrorPulseRootRouter(paths.SyncRootPath, topology.Roots);
        var directorySource = new MirrorPulseAdapterDirectoryPageSource(workers);
        var provider = new MirrorPulseDemandProvider(rootRouter, workers, directorySource);
        await using var session = MirrorPulseCloudHostSession.CreateDefault(
            paths, topology.Instances, topology.Roots, provider, workers, workers,
            rootRouter, catalog, instanceId => topology.Instances.Any(instance =>
                instance.InstanceId == instanceId && instance.Enabled),
            conflictCenter, conflictNotifications, workers);
        currentSession = session;
        await session.StartAsync(shutdown.Token);
        await workers.StartAsync(topology);
        await using var remotePoller = new MirrorPulseActiveRemotePoller(directorySource,
            topology.Instances, topology.Roots, ApplyCloudRemoteBatchAsync);
        await remotePoller.StartAsync(shutdown.Token);
        Console.WriteLine($"{ProductInfo.Name} Cloud Files session started at {paths.SyncRootPath}.");
        if (args.Length == 0)
        {
            async Task<MirrorPulseAppStatusResponse> ReadStatusAsync(CancellationToken cancellationToken)
            {
                MirrorPulseAdapterTopology current = await catalog.ReadAdapterTopologyAsync(cancellationToken);
                MirrorPulseCloudStatusSnapshot cloud = await session.ReadStatusAsync(
                    current.Instances.Select(instance => instance.InstanceId), cancellationToken);
                var entries = new List<MirrorPulseAppInstanceStatus>(current.Instances.Count);
                foreach (var instance in current.Instances)
                {
                    MirrorPulseInstanceRuntimeState? runtime = await catalog.ReadInstanceRuntimeStateAsync(
                        instance.InstanceId, cancellationToken);
                    MirrorPulseInstanceCursorStatus? cursor = cloud.Cursors.SingleOrDefault(item =>
                        item.InstanceId == instance.InstanceId);
                    entries.Add(new MirrorPulseAppInstanceStatus(instance.InstanceId.ToString(),
                        instance.DisplayName, instance.Enabled,
                        instance.Enabled ? runtime?.Phase ?? "Not running" : "Offline",
                        cursor?.CursorFingerprint, cursor?.UpdatedAt, runtime?.LastSuccessfulSync,
                        runtime?.LastErrorCode, runtime?.TransferProgress));
                }

                IReadOnlySet<Guid> snoozed = await catalog.ReadSnoozedConflictIdsAsync(cancellationToken);
                IReadOnlyList<MirrorPulseConflictRecord> uploadConflicts =
                    await catalog.ReadUploadConflictsAsync(cancellationToken: cancellationToken);
                var remoteNotifications = (await catalog.ReadRemoteConflictProjectionsAsync(cancellationToken))
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

            async Task<MirrorPulseAppStatusResponse> InstallAdapterAsync(
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
                string installationRoot = Path.Combine(paths.DataRootPath, "adapters", "installed");
                InstalledAdapter installed = await catalog.InstallSignedAdapterAsync(
                    source, signaturePath, installationRoot, runtimeIdentifier, cancellationToken)
                    .ConfigureAwait(false);
                return (await ReadStatusAsync(cancellationToken).ConfigureAwait(false)) with
                {
                    InstalledAdapterId = installed.InstallId.ToString(),
                };
            }

            var provisioner = new MirrorPulseAdapterInstanceProvisioner(catalog,
                credentialStore, paths.DataRootPath);
            async Task<MirrorPulseAppStatusResponse> CreateInstanceAsync(
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

            var statusPipe = new MirrorPulseAppStatusPipe(ReadStatusAsync,
                async (conflictId, cancellationToken) =>
                {
                    MirrorPulseAppStatusResponse current = await ReadStatusAsync(cancellationToken);
                    if (!current.Notifications.Any(item => item.ConflictId == conflictId.ToString("D")))
                    {
                        throw new FileNotFoundException("The pending conflict notification was not found.");
                    }

                    await catalog.SetConflictSnoozedAsync(conflictId, true, cancellationToken);
                    return await ReadStatusAsync(cancellationToken);
                },
                async (instanceId, enabled, cancellationToken) =>
                {
                    await catalog.SetInstanceEnabledAsync(instanceId, enabled, cancellationToken);
                    return await ReadStatusAsync(cancellationToken);
                },
                async (instanceId, installId, cancellationToken) =>
                {
                    await catalog.SelectInstanceInstallationAsync(instanceId, installId, cancellationToken);
                    return await ReadStatusAsync(cancellationToken);
                },
                InstallAdapterAsync, CreateInstanceAsync,
                async (conflictId, action, cancellationToken) =>
                {
                    if (await catalog.ReadUploadConflictAsync(conflictId, cancellationToken)
                        .ConfigureAwait(false) is not null)
                    {
                        await session.ApplyUploadConflictAsync(conflictId, action, cancellationToken)
                            .ConfigureAwait(false);
                        conflictCenter.Remove(conflictId);
                    }
                    else
                    {
                        MirrorPulseRemoteConflictActionOutcome outcome =
                            await session.ApplyRemoteConflictAsync(conflictId, action, catalog,
                                cancellationToken).ConfigureAwait(false);
                        if (outcome.Resolved)
                        {
                            conflictCenter.Remove(conflictId);
                        }
                    }

                    return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
                });
            await statusPipe.ServeAsync(shutdown.Token);
        }

        return 0;
    }
    catch (OperationCanceledException) when (shutdown.IsCancellationRequested)
    {
        return 0;
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"{ProductInfo.Name} Host failed: {exception.Message}");
    return 1;
}

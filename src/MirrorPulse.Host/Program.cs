using System.Text.Json;
using CfSharp;
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
        async ValueTask ApplyRemoteBatchAsync(
            InstanceId instanceId,
            JsonElement payload,
            CancellationToken cancellationToken)
        {
            CloudRemoteChangeBatch batch = payload.Deserialize<CloudRemoteChangeBatch>()
                ?? throw new InvalidDataException("The Adapter remote batch payload is empty.");
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

        await using var workers = new AdapterInstanceProcessSupervisor(catalog,
            new WindowsCredentialManagerStore(), ApplyRemoteBatchAsync);
        var rootRouter = new MirrorPulseRootRouter(paths.SyncRootPath, topology.Roots);
        var provider = new MirrorPulseDemandProvider(rootRouter, workers);
        await using var session = MirrorPulseCloudHostSession.CreateDefault(
            paths, topology.Instances, topology.Roots, provider);
        currentSession = session;
        await session.StartAsync(shutdown.Token);
        await workers.StartAsync(topology);
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

                IReadOnlySet<Guid> snoozed = await catalog.ReadSnoozedRemoteConflictIdsAsync(cancellationToken);
                var notifications = (await catalog.ReadRemoteConflictProjectionsAsync(cancellationToken))
                    .Where(conflict => cloud.PendingRemoteConflictIds.Contains(conflict.ConflictId))
                    .Select(conflict => new MirrorPulseAppNotification(
                        conflict.ConflictId.ToString("D"), conflict.RelativePath,
                        conflict.DetectedAt, snoozed.Contains(conflict.ConflictId)))
                    .OrderByDescending(item => item.DetectedAt)
                    .ToArray();
                return new MirrorPulseAppStatusResponse(cloud.PendingUploadCount,
                    cloud.PendingRemoteConflictCount, entries, notifications);
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
                if (!File.Exists(signaturePath))
                {
                    throw new FileNotFoundException(
                        "The detached Adapter signature must be next to the .mpadapter file.", signaturePath);
                }

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
                string displayName = installed.Manifest.LocaleMetadata.TryGetValue("en-US", out AdapterLocaleMetadata? locale)
                    ? locale.DisplayName
                    : installed.Manifest.AdapterId.ToString();
                string instanceCacheRoot = Path.Combine(paths.DataRootPath, "adapters", "instances",
                    Guid.NewGuid().ToString("D"));
                await catalog.CreateInstanceAsync(
                    installed.InstallId,
                    displayName,
                    new Dictionary<string, string>(StringComparer.Ordinal),
                    Array.Empty<string>(),
                    Path.Combine(instanceCacheRoot, "files"),
                    Path.Combine(instanceCacheRoot, "transfers"),
                    enabled: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return await ReadStatusAsync(cancellationToken).ConfigureAwait(false);
            }

            var statusPipe = new MirrorPulseAppStatusPipe(ReadStatusAsync,
                async (conflictId, cancellationToken) =>
                {
                    MirrorPulseAppStatusResponse current = await ReadStatusAsync(cancellationToken);
                    if (!current.Notifications.Any(item => item.ConflictId == conflictId.ToString("D")))
                    {
                        throw new FileNotFoundException("The pending conflict notification was not found.");
                    }

                    await catalog.SetRemoteConflictSnoozedAsync(conflictId, true, cancellationToken);
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
                InstallAdapterAsync);
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

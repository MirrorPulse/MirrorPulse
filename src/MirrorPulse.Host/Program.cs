using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.State;

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
    using var shutdown = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };
    Console.CancelKeyPress += cancel;
    try
    {
        await using var session = MirrorPulseCloudHostSession.CreateDefault(
            paths, topology.Instances, topology.Roots);
        await session.StartAsync(shutdown.Token);
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
                        cursor?.CursorFingerprint, cursor?.UpdatedAt, runtime?.LastSuccessfulSync));
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

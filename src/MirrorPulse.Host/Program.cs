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
            var statusPipe = new MirrorPulseAppStatusPipe(async cancellationToken =>
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

                return new MirrorPulseAppStatusResponse(cloud.PendingUploadCount,
                    cloud.PendingRemoteConflictCount, entries);
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

using MirrorPulse.Core;
using MirrorPulse.Core.Configuration;
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

string dataRoot = Environment.GetEnvironmentVariable("MIRRORPULSE_DATA_ROOT") is { Length: > 0 } configuredDataRoot
    ? Path.GetFullPath(configuredDataRoot)
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductInfo.Name);
string syncRoot = Environment.GetEnvironmentVariable("MIRRORPULSE_SYNC_ROOT") is { Length: > 0 } configuredSyncRoot
    ? Path.GetFullPath(configuredSyncRoot)
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ProductInfo.Name);
var paths = new MirrorPulseStoragePaths(syncRoot, dataRoot);

try
{
    await using var application = await MirrorPulseHostApplication.CreateAsync(paths);
    using var shutdown = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };
    Console.CancelKeyPress += cancel;
    try
    {
        await application.StartAsync(shutdown.Token);
        Console.WriteLine($"{ProductInfo.Name} Cloud Files session started at {paths.SyncRootPath}.");
        if (args.Length == 0)
        {
            await application.ServeAsync(shutdown.Token);
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

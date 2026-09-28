using CfSharp;
using CfSharp.Storage.Sqlite;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Connects MP's single sync-root path policy to the official CfSharp SQLite provider.
/// </summary>
public static class MirrorPulseCfSharpStateStoreFactory
{
    public static ICloudStateStoreFactory Create(MirrorPulseStoragePaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return new SqliteCloudStateStoreFactory(paths.CfSharpStateDatabasePath);
    }
}

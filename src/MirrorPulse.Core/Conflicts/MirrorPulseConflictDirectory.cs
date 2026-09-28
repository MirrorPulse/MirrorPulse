using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Allocates one MP-owned conflict directory per Adapter instance outside the sync root.
/// </summary>
public static class MirrorPulseConflictDirectory
{
    public const string DirectoryName = "conflicts";

    public static string GetPath(MirrorPulseStoragePaths paths, InstanceId instanceId)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Path.Combine(paths.DataRootPath, DirectoryName, instanceId.ToString());
    }

    public static string EnsureExists(MirrorPulseStoragePaths paths, InstanceId instanceId)
    {
        var path = GetPath(paths, instanceId);
        Directory.CreateDirectory(path);
        return path;
    }
}

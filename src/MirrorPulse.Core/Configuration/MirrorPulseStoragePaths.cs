using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Configuration;

/// <summary>
/// The user-visible Cloud Files root and MP's private persistent data root.
/// </summary>
public sealed record MirrorPulseStoragePaths
{
    public MirrorPulseStoragePaths(string syncRootPath, string dataRootPath)
    {
        SyncRootPath = SourceDirectoryPathNormalizer.Normalize(syncRootPath);
        DataRootPath = SourceDirectoryPathNormalizer.Normalize(dataRootPath);
        if (SourceDirectoryPathNormalizer.IsWithin(SyncRootPath, DataRootPath)
            || SourceDirectoryPathNormalizer.IsWithin(DataRootPath, SyncRootPath))
        {
            throw new ArgumentException("The Cloud Files sync root and MP data root must not overlap.", nameof(dataRootPath));
        }
    }

    public string SyncRootPath { get; }

    public string DataRootPath { get; }
}

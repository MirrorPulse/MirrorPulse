using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Configuration;

/// <summary>
/// The user-visible Cloud Files root and MP's private persistent data root.
/// </summary>
public sealed record MirrorPulseStoragePaths
{
    public const string CfSharpDatabaseFileName = "cfsharp.db";

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

    /// <summary>
    /// The one durable CfSharp database for the MirrorPulse sync root. The official provider
    /// performs the final physical path and ownership checks when opening this database.
    /// </summary>
    public string CfSharpStateDatabasePath => Path.Combine(DataRootPath, "state", CfSharpDatabaseFileName);
}

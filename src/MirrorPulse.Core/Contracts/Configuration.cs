using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Version-one MP configuration owned by the current user's profile.
/// </summary>
public sealed record MirrorPulseConfiguration
{
    public const int CurrentSchemaVersion = 1;

    public MirrorPulseConfiguration(
        int schemaVersion,
        string locale,
        bool developerMode,
        bool startWithWindows,
        IReadOnlyList<InstallId> enabledInstallations,
        string syncRootDisplayName = "MirrorPulse")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentNullException.ThrowIfNull(enabledInstallations);
        ArgumentException.ThrowIfNullOrWhiteSpace(syncRootDisplayName);
        if (enabledInstallations.Distinct().Count() != enabledInstallations.Count)
        {
            throw new ArgumentException("Enabled installations must be unique.", nameof(enabledInstallations));
        }

        SchemaVersion = schemaVersion;
        Locale = locale;
        DeveloperMode = developerMode;
        StartWithWindows = startWithWindows;
        EnabledInstallations = new ReadOnlyCollection<InstallId>(enabledInstallations.ToArray());
        SyncRootDisplayName = syncRootDisplayName;
    }

    public int SchemaVersion { get; }

    public string Locale { get; }

    public bool DeveloperMode { get; }

    public bool StartWithWindows { get; }

    /// <summary>
    /// Installed Adapter copies that MP starts in the current user session.
    /// </summary>
    public IReadOnlyList<InstallId> EnabledInstallations { get; }

    public string SyncRootDisplayName { get; }
}

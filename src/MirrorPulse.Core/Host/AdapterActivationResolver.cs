using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public sealed record AdapterActivationSnapshot
{
    public AdapterActivationSnapshot(
        IReadOnlyList<InstallId> enabled,
        IReadOnlyList<InstallId> offlineDisabled,
        IReadOnlyList<InstallId> missing)
    {
        Enabled = new ReadOnlyCollection<InstallId>(enabled.ToArray());
        OfflineDisabled = new ReadOnlyCollection<InstallId>(offlineDisabled.ToArray());
        Missing = new ReadOnlyCollection<InstallId>(missing.ToArray());
    }

    public IReadOnlyList<InstallId> Enabled { get; }

    public IReadOnlyList<InstallId> OfflineDisabled { get; }

    public IReadOnlyList<InstallId> Missing { get; }

    public bool IsEnabled(InstallId installId) => Enabled.Contains(installId);
}

/// <summary>
/// Converts MP's enabled installation list into online and offline runtime sets.
/// </summary>
public static class AdapterActivationResolver
{
    public static AdapterActivationSnapshot Resolve(
        MirrorPulseConfiguration configuration,
        IEnumerable<InstallId> installedInstallations)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(installedInstallations);
        var installed = installedInstallations.Distinct().ToArray();
        var enabled = configuration.EnabledInstallations.Where(installed.Contains).ToArray();
        var enabledSet = enabled.ToHashSet();
        var offline = installed.Where(installId => !enabledSet.Contains(installId)).ToArray();
        var installedSet = installed.ToHashSet();
        var missing = configuration.EnabledInstallations.Where(installId => !installedSet.Contains(installId)).ToArray();
        return new AdapterActivationSnapshot(enabled, offline, missing);
    }
}

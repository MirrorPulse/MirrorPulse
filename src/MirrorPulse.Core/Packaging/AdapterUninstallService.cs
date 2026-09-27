using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterUninstallResult(InstallId InstallId, bool InstallationRemoved, bool ActivePointerCleared);

/// <summary>
/// Removes one installed Adapter copy while preserving other versions and installations.
/// </summary>
public sealed class AdapterUninstallService
{
    private readonly CurrentUserAdapterPathProvider _paths;
    private readonly AdapterActivationPointerStore _activation;

    public AdapterUninstallService(
        CurrentUserAdapterPathProvider paths,
        AdapterActivationPointerStore activation)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(activation);
        _paths = paths;
        _activation = activation;
    }

    public async Task<AdapterUninstallResult> UninstallAsync(
        InstalledAdapter installedAdapter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(installedAdapter);
        var expected = Path.GetFullPath(_paths.GetInstallationDirectory(
            installedAdapter.AdapterId,
            installedAdapter.Version,
            installedAdapter.InstallId));
        var actual = Path.GetFullPath(installedAdapter.InstallationDirectory);
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The installed Adapter path is outside the managed installation layout.", nameof(installedAdapter));
        }

        var removed = Directory.Exists(actual);
        if (removed)
        {
            Directory.Delete(actual, recursive: true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var active = await _activation.ReadAsync(installedAdapter.AdapterId, cancellationToken).ConfigureAwait(false);
        var pointerCleared = active?.InstallId == installedAdapter.InstallId;
        if (pointerCleared)
        {
            var pointerPath = _activation.GetPointerPath(installedAdapter.AdapterId);
            if (File.Exists(pointerPath))
            {
                File.Delete(pointerPath);
            }
        }

        return new AdapterUninstallResult(installedAdapter.InstallId, removed, pointerCleared);
    }
}

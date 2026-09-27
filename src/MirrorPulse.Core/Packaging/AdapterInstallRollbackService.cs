using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterRollbackResult(bool FailedInstallationRemoved, bool PreviousInstallationRestored);

/// <summary>
/// Removes a failed installation and restores the last known active version.
/// </summary>
public sealed class AdapterInstallRollbackService
{
    private readonly CurrentUserAdapterPathProvider _paths;
    private readonly AdapterActivationPointerStore _activation;

    public AdapterInstallRollbackService(
        CurrentUserAdapterPathProvider paths,
        AdapterActivationPointerStore activation)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(activation);
        _paths = paths;
        _activation = activation;
    }

    public async Task<AdapterRollbackResult> RollbackAsync(
        AdapterId adapterId,
        string failedVersion,
        InstallId failedInstallId,
        ActiveAdapterInstallation? previous,
        CancellationToken cancellationToken = default)
    {
        var failedDirectory = _paths.GetInstallationDirectory(adapterId, failedVersion, failedInstallId);
        if (Directory.Exists(failedDirectory))
        {
            Directory.Delete(failedDirectory, recursive: true);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (previous is not null)
        {
            if (previous.AdapterId != adapterId ||
                !Directory.Exists(_paths.GetInstallationDirectory(adapterId, previous.Version, previous.InstallId)))
            {
                throw new DirectoryNotFoundException("The previous Adapter installation is not available for rollback.");
            }

            await _activation.ActivateAsync(adapterId, previous.Version, previous.InstallId, cancellationToken).ConfigureAwait(false);
            return new AdapterRollbackResult(true, true);
        }

        var pointerPath = _activation.GetPointerPath(adapterId);
        if (File.Exists(pointerPath))
        {
            File.Delete(pointerPath);
        }

        return new AdapterRollbackResult(true, false);
    }
}

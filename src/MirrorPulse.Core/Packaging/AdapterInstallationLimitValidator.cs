using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterInstallationLimitResult(
    bool Allowed,
    int ExistingInstallations,
    int? MaximumInstallations,
    string? ErrorCode);

/// <summary>
/// Applies the package-declared installation multiplicity limit before staging.
/// </summary>
public static class AdapterInstallationLimitValidator
{
    public static AdapterInstallationLimitResult Validate(AdapterManifest manifest, int existingInstallations)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentOutOfRangeException.ThrowIfNegative(existingInstallations);
        var maximum = manifest.InstallPolicy.MaximumInstallations;
        if (maximum is null)
        {
            return new AdapterInstallationLimitResult(true, existingInstallations, null, null);
        }

        if (maximum <= 0)
        {
            return new AdapterInstallationLimitResult(false, existingInstallations, maximum, "manifest.installPolicy.invalid");
        }

        return existingInstallations < maximum
            ? new AdapterInstallationLimitResult(true, existingInstallations, maximum, null)
            : new AdapterInstallationLimitResult(false, existingInstallations, maximum, "install.maxInstallationsReached");
    }
}

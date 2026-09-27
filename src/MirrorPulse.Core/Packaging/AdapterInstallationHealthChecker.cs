using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterInstallationHealth(bool IsHealthy, IReadOnlyList<string> Problems);

/// <summary>
/// Performs a non-executing health check on an installed Adapter payload.
/// </summary>
public sealed class AdapterInstallationHealthChecker
{
    public static AdapterInstallationHealth Check(InstalledAdapter installedAdapter, string runtimeIdentifier)
    {
        ArgumentNullException.ThrowIfNull(installedAdapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        var problems = new List<string>();
        var directory = Path.GetFullPath(installedAdapter.InstallationDirectory);
        if (!Directory.Exists(directory))
        {
            problems.Add("installation.directory.missing");
        }

        if (!installedAdapter.Manifest.Entrypoints.TryGetValue(runtimeIdentifier, out var entrypoint) ||
            string.IsNullOrWhiteSpace(entrypoint))
        {
            problems.Add("installation.entrypoint.missing");
        }
        else if (!IsSafePayloadPath(entrypoint))
        {
            problems.Add("installation.entrypoint.unsafe");
        }
        else
        {
            var executable = Path.GetFullPath(Path.Combine(directory, entrypoint.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsUnderDirectory(executable, directory) || !File.Exists(executable))
            {
                problems.Add("installation.entrypoint.fileMissing");
            }
        }

        return new AdapterInstallationHealth(problems.Count == 0, problems.AsReadOnly());
    }

    private static bool IsSafePayloadPath(string path) =>
        !Path.IsPathRooted(path) && !path.Contains('\\') &&
        path.Split('/').All(segment => segment is not ("." or ".."));

    private static bool IsUnderDirectory(string path, string directory)
    {
        var root = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}

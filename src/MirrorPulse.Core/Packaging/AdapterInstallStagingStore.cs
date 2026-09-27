using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Packaging;

public sealed record AdapterInstallStage(InstallId InstallId, string DirectoryPath, string PackagePath);

/// <summary>
/// Owns the short-lived current-user staging area used before package validation.
/// </summary>
public sealed class AdapterInstallStagingStore
{
    private readonly string _rootDirectory;

    public AdapterInstallStagingStore(string? rootDirectory = null)
    {
        _rootDirectory = Path.GetFullPath(rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MirrorPulse",
            "adapters",
            "staging"));
    }

    public string RootDirectory => _rootDirectory;

    public async Task<AdapterInstallStage> StageAsync(
        InstallId installId,
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        var source = Path.GetFullPath(packagePath);
        if (!File.Exists(source))
        {
            throw new FileNotFoundException("The Adapter package was not found.", source);
        }

        if (!string.Equals(Path.GetExtension(source), ".mpadapter", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only .mpadapter packages can be staged.", nameof(packagePath));
        }

        var directory = Path.Combine(_rootDirectory, installId.ToString());
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, "package.mpadapter");
        var temporary = target + ".tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, target, overwrite: true);
            return new AdapterInstallStage(installId, directory, target);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public void Remove(AdapterInstallStage stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        var directory = Path.GetFullPath(stage.DirectoryPath);
        if (!IsUnderRoot(directory))
        {
            throw new ArgumentException("The staging directory is outside the staging root.", nameof(stage));
        }

        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private bool IsUnderRoot(string path)
    {
        var root = _rootDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return path.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}

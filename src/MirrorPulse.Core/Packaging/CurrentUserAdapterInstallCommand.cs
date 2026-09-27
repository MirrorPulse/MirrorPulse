namespace MirrorPulse.Core.Packaging;

public sealed record AdapterInstallReceipt(
    Guid InstallId,
    string PackagePath,
    bool RequiresSignatureVerification,
    DateTimeOffset ReceivedAt);

/// <summary>
/// Receives a local `.mpadapter` file into the current user's installation inbox.
/// Verification and activation happen in later installation stages.
/// </summary>
public sealed class CurrentUserAdapterInstallCommand
{
    private readonly string _inboxDirectory;

    public CurrentUserAdapterInstallCommand(string? inboxDirectory = null)
    {
        _inboxDirectory = Path.GetFullPath(inboxDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MirrorPulse",
            "adapters",
            "inbox"));
    }

    public string InboxDirectory => _inboxDirectory;

    public async Task<AdapterInstallReceipt> ExecuteAsync(
        string packagePath,
        bool developerMode,
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
            throw new ArgumentException("The local install command accepts only .mpadapter files.", nameof(packagePath));
        }

        Directory.CreateDirectory(_inboxDirectory);
        var installId = Guid.NewGuid();
        var installDirectory = Path.Combine(_inboxDirectory, installId.ToString("D"));
        Directory.CreateDirectory(installDirectory);
        var target = Path.Combine(installDirectory, "package.mpadapter");
        var temporary = target + ".tmp";
        try
        {
            await using (var input = File.OpenRead(source))
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            {
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, target);
            return new AdapterInstallReceipt(installId, target, !developerMode, DateTimeOffset.UtcNow);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}

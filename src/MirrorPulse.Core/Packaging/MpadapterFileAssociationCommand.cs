namespace MirrorPulse.Core.Packaging;

/// <summary>
/// Entry point used by the Windows `.mpadapter` file association.
/// </summary>
public sealed class MpadapterFileAssociationCommand
{
    private readonly CurrentUserAdapterInstallCommand _installCommand;

    public MpadapterFileAssociationCommand(CurrentUserAdapterInstallCommand? installCommand = null)
    {
        _installCommand = installCommand ?? new CurrentUserAdapterInstallCommand();
    }

    public Task<AdapterInstallReceipt> ExecuteAsync(
        string packagePath,
        bool developerMode,
        CancellationToken cancellationToken = default) =>
        _installCommand.ExecuteAsync(packagePath, developerMode, cancellationToken);

    public Task<AdapterInstallReceipt> ExecuteAsync(
        IReadOnlyList<string> fileAssociationArguments,
        bool developerMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileAssociationArguments);
        if (fileAssociationArguments.Count != 1)
        {
            throw new ArgumentException("The .mpadapter file association requires exactly one package path.", nameof(fileAssociationArguments));
        }

        return ExecuteAsync(fileAssociationArguments[0], developerMode, cancellationToken);
    }
}

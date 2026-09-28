namespace MirrorPulse.Core.Adapters.LocalDirectory;

public enum MirrorPulseLocalDirectoryMutationKind
{
    Delete,
    Move,
}

public sealed record MirrorPulseLocalDirectoryMutationResult(
    MirrorPulseLocalDirectoryMutationKind Kind,
    string RelativePath,
    string? PreviousRelativePath,
    bool Changed);

/// <summary>
/// Applies safe local-directory delete and move operations below one authorized source root.
/// </summary>
public sealed class MirrorPulseLocalDirectoryMutator
{
    private readonly string _sourceDirectory;

    public MirrorPulseLocalDirectoryMutator(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        _sourceDirectory = Path.GetFullPath(sourceDirectory);
    }

    public Task<MirrorPulseLocalDirectoryMutationResult> DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, relativePath);
        var changed = false;
        if (File.Exists(path))
        {
            File.Delete(path);
            changed = true;
        }
        else if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
            changed = true;
        }

        return Task.FromResult(new MirrorPulseLocalDirectoryMutationResult(
            MirrorPulseLocalDirectoryMutationKind.Delete,
            Normalize(relativePath),
            null,
            changed));
    }

    public Task<MirrorPulseLocalDirectoryMutationResult> MoveAsync(
        string sourceRelativePath,
        string destinationRelativePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var source = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, sourceRelativePath);
        var destination = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, destinationRelativePath);
        if (!File.Exists(source) && !Directory.Exists(source))
        {
            throw new FileNotFoundException("The local move source was not found.", source);
        }

        if (File.Exists(destination) || Directory.Exists(destination))
        {
            throw new IOException("The local move destination already exists.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        if (File.Exists(source))
        {
            File.Move(source, destination);
        }
        else
        {
            Directory.Move(source, destination);
        }

        return Task.FromResult(new MirrorPulseLocalDirectoryMutationResult(
            MirrorPulseLocalDirectoryMutationKind.Move,
            Normalize(destinationRelativePath),
            Normalize(sourceRelativePath),
            true));
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/').Replace('\\', '/');
}

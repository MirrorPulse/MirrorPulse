namespace MirrorPulse.Core.Adapters.LocalDirectory;

public static class MirrorPulseLocalDirectoryPath
{
    public static string Resolve(string sourceDirectory, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\0'))
        {
            throw new ArgumentException("A local Adapter path must be relative and null-free.", nameof(relativePath));
        }

        var root = Path.GetFullPath(sourceDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A local Adapter path must remain below its source directory.", nameof(relativePath));
        }

        return resolved;
    }
}

/// <summary>
/// Reads complete files or bounded ranges from a local Adapter source directory.
/// </summary>
public sealed class MirrorPulseLocalDirectoryReader
{
    private readonly string _sourceDirectory;

    public MirrorPulseLocalDirectoryReader(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        _sourceDirectory = Path.GetFullPath(sourceDirectory);
    }

    public string SourceDirectory => _sourceDirectory;

    public Task<byte[]> ReadAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var path = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, relativePath);
        return File.ReadAllBytesAsync(path, cancellationToken);
    }

    public async Task<byte[]> ReadRangeAsync(
        string relativePath,
        long offset,
        int length,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        var path = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, relativePath);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        if (offset > stream.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "The requested offset is past the end of the file.");
        }

        stream.Position = offset;
        var buffer = new byte[(int)Math.Min(length, stream.Length - offset)];
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        return read == buffer.Length ? buffer : buffer[..read];
    }
}

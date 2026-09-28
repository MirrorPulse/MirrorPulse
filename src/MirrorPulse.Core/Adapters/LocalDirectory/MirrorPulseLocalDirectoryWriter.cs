namespace MirrorPulse.Core.Adapters.LocalDirectory;

public sealed record MirrorPulseLocalDirectoryWriteResult(string RelativePath, long Length);

/// <summary>
/// Writes local Adapter files through same-directory temporary files and atomic replacement.
/// </summary>
public sealed class MirrorPulseLocalDirectoryWriter
{
    private readonly string _sourceDirectory;

    public MirrorPulseLocalDirectoryWriter(string sourceDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        _sourceDirectory = Path.GetFullPath(sourceDirectory);
    }

    public async Task<MirrorPulseLocalDirectoryWriteResult> WriteAsync(
        string relativePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var destination = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".{Guid.NewGuid():N}.mp-tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, destination, overwrite: true);
            return new MirrorPulseLocalDirectoryWriteResult(
                Path.GetRelativePath(_sourceDirectory, destination).Replace(Path.DirectorySeparatorChar, '/'),
                content.Length);
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

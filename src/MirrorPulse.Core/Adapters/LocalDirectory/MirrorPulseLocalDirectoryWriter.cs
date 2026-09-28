namespace MirrorPulse.Core.Adapters.LocalDirectory;

public sealed record MirrorPulseLocalDirectoryWriteResult(string RelativePath, long Length);

/// <summary>
/// Writes local Adapter files through same-directory temporary files and atomic replacement.
/// </summary>
public sealed class MirrorPulseLocalDirectoryWriter
{
    private readonly string _sourceDirectory;
    private readonly MirrorPulseLocalDirectoryEchoSuppressor? _echo;

    public MirrorPulseLocalDirectoryWriter(
        string sourceDirectory,
        MirrorPulseLocalDirectoryEchoSuppressor? echo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        _sourceDirectory = Path.GetFullPath(sourceDirectory);
        _echo = echo;
    }

    public async Task<MirrorPulseLocalDirectoryWriteResult> WriteAsync(
        string relativePath,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        var destination = MirrorPulseLocalDirectoryPath.Resolve(_sourceDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + $".{Guid.NewGuid():N}.mp-tmp";
        if (_echo is not null)
        {
            DateTimeOffset expiry = DateTimeOffset.UtcNow.AddSeconds(10);
            string targetRelative = Path.GetRelativePath(_sourceDirectory, destination);
            string temporaryRelative = Path.GetRelativePath(_sourceDirectory, temporary);
            foreach (MirrorPulseLocalDirectoryChangeKind kind in Enum.GetValues<MirrorPulseLocalDirectoryChangeKind>())
            {
                _echo.Register(targetRelative, kind, expiry, 4);
                _echo.Register(temporaryRelative, kind, expiry, 4);
            }
        }

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

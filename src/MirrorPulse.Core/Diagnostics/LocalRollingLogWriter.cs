using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Diagnostics;

/// <summary>
/// Writes structured logs to local JSON lines and rotates bounded files.
/// </summary>
public sealed class LocalRollingLogWriter : IDisposable
{
    private readonly string _path;
    private readonly long _maxFileBytes;
    private readonly int _backupCount;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LocalRollingLogWriter(string directory, long maxFileBytes = 4 * 1024 * 1024, int backupCount = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxFileBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(backupCount, 1);

        Directory.CreateDirectory(directory);
        _path = Path.Combine(Path.GetFullPath(directory), "mirrorpulse.log");
        _maxFileBytes = maxFileBytes;
        _backupCount = backupCount;
    }

    public string FilePath => _path;

    public async Task WriteAsync(LogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var line = JsonSerializer.SerializeToUtf8Bytes(new
        {
            entry.OccurredAt,
            Level = entry.Level.ToString(),
            entry.Category,
            entry.Message,
            Fields = entry.Fields.ToDictionary(field => field.Name, field => field.Value, StringComparer.Ordinal),
        });
        if (line.LongLength + 1 > _maxFileBytes)
        {
            throw new InvalidDataException("The log entry exceeds the maximum log file size.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(_path) && new FileInfo(_path).Length + line.LongLength + 1 > _maxFileBytes)
            {
                Rotate();
            }

            await using var stream = new FileStream(_path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.Asynchronous);
            await stream.WriteAsync(line, cancellationToken).ConfigureAwait(false);
            await stream.WriteAsync("\n"u8.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();

    private void Rotate()
    {
        var oldest = _path + $".{_backupCount}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var number = _backupCount - 1; number >= 1; number--)
        {
            var previous = _path + $".{number}";
            if (File.Exists(previous))
            {
                File.Move(previous, _path + $".{number + 1}");
            }
        }

        File.Move(_path, _path + ".1");
    }
}

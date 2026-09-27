using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Diagnostics;

/// <summary>
/// Exports local diagnostics and already-redacted log files without contacting a remote service.
/// </summary>
public sealed class DiagnosticPackageExporter
{
    public static async Task<string> ExportAsync(
        string outputPath,
        IEnumerable<DiagnosticEvent> events,
        IEnumerable<string> logFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(logFiles);
        var output = Path.GetFullPath(outputPath);
        if (!string.Equals(Path.GetExtension(output), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Diagnostic packages must use the .zip extension.", nameof(outputPath));
        }

        var directory = Path.GetDirectoryName(output) ?? throw new InvalidOperationException("The diagnostic package has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = output + $".{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.Asynchronous))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var eventEntry = archive.CreateEntry("diagnostics.jsonl", CompressionLevel.Optimal);
                {
                    await using var eventStream = eventEntry.Open();
                    await using var writer = new StreamWriter(eventStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: false);
                    foreach (var diagnosticEvent in events)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var sanitized = new
                        {
                            diagnosticEvent.EventId,
                            diagnosticEvent.Source,
                            diagnosticEvent.Diagnostic,
                            diagnosticEvent.OccurredAt,
                            diagnosticEvent.CorrelationId,
                            Properties = diagnosticEvent.Properties.ToDictionary(
                                property => property.Key,
                                property => LogFieldPolicy.Redact(property.Key, property.Value),
                                StringComparer.Ordinal),
                        };
                        await writer.WriteLineAsync(JsonSerializer.Serialize(sanitized).AsMemory(), cancellationToken).ConfigureAwait(false);
                    }

                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var logFile in logFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = Path.GetFullPath(logFile);
                    if (!File.Exists(source))
                    {
                        continue;
                    }

                    var baseName = Path.GetFileName(source);
                    var entryName = $"logs/{baseName}";
                    var suffix = 1;
                    while (!names.Add(entryName))
                    {
                        entryName = $"logs/{Path.GetFileNameWithoutExtension(baseName)}-{suffix++}{Path.GetExtension(baseName)}";
                    }

                    var logEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var input = File.OpenRead(source);
                    await using var destination = logEntry.Open();
                    await input.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(temporary, output, overwrite: true);
            return output;
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

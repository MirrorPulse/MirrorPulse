using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Diagnostics;

/// <summary>
/// Exports only allowlisted local diagnostic fields without contacting a remote service.
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
                        SafeDiagnosticEvent sanitized = SafeDiagnosticPolicy.Sanitize(diagnosticEvent);
                        await writer.WriteLineAsync(JsonSerializer.Serialize(sanitized).AsMemory(), cancellationToken).ConfigureAwait(false);
                    }

                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                int logIndex = 0;
                foreach (var logFile in logFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var source = Path.GetFullPath(logFile);
                    if (!File.Exists(source))
                    {
                        continue;
                    }

                    string entryName = logIndex == 0 ? "logs/mirrorpulse.log" : $"logs/mirrorpulse-{logIndex:D3}.log";
                    logIndex++;

                    var logEntry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                    await using var destination = logEntry.Open();
                    await CopySanitizedLogAsync(source, destination, cancellationToken).ConfigureAwait(false);
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

    private static async Task CopySanitizedLogAsync(
        string source,
        Stream destination,
        CancellationToken cancellationToken)
    {
        using var input = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        await using var output = new StreamWriter(destination, new UTF8Encoding(false), leaveOpen: true);
        while (await input.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string sanitized;
            try
            {
                using JsonDocument document = JsonDocument.Parse(line);
                sanitized = JsonSerializer.Serialize(SafeDiagnosticPolicy.SanitizeLegacyLog(document.RootElement));
            }
            catch (JsonException)
            {
                sanitized = JsonSerializer.Serialize(SafeDiagnosticPolicy.SanitizeLegacyLog(default));
            }
            catch (InvalidDataException)
            {
                sanitized = JsonSerializer.Serialize(SafeDiagnosticPolicy.SanitizeLegacyLog(default));
            }

            await output.WriteLineAsync(sanitized.AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

}

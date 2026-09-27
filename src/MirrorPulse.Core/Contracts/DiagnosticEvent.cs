using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Local diagnostic event emitted by MP or a Worker supervisor.
/// </summary>
public sealed record DiagnosticEvent
{
    public DiagnosticEvent(
        Guid eventId,
        string source,
        Diagnostic diagnostic,
        DateTimeOffset occurredAt,
        string? correlationId = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        if (eventId == Guid.Empty)
        {
            throw new ArgumentException("A diagnostic event ID cannot be empty.", nameof(eventId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        ArgumentNullException.ThrowIfNull(diagnostic);
        EventId = eventId;
        Source = source;
        Diagnostic = diagnostic;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
        Properties = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(properties ?? new Dictionary<string, string>(), StringComparer.Ordinal));
    }

    public Guid EventId { get; }

    public string Source { get; }

    public Diagnostic Diagnostic { get; }

    public DateTimeOffset OccurredAt { get; }

    public string? CorrelationId { get; }

    public IReadOnlyDictionary<string, string> Properties { get; }
}

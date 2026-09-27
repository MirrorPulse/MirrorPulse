using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Central policy for recognizing fields that must never retain secret values.
/// </summary>
public static class LogFieldPolicy
{
    public const string RedactedValue = "[REDACTED]";

    public static bool IsSensitiveName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            normalized.Contains("credentialvalue", StringComparison.OrdinalIgnoreCase);
    }

    public static string Redact(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        return IsSensitiveName(name) ? RedactedValue : value;
    }
}

/// <summary>
/// One structured log field with sensitive values redacted at construction.
/// </summary>
public sealed record LogField
{
    public LogField(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);
        Name = name;
        IsSensitive = LogFieldPolicy.IsSensitiveName(name);
        Value = LogFieldPolicy.Redact(name, value);
    }

    public string Name { get; }

    public string Value { get; }

    public bool IsSensitive { get; }
}

/// <summary>
/// Structured local log entry. Callers must keep secret material in LogField values.
/// </summary>
public sealed record LogEntry
{
    public LogEntry(
        LogLevel level,
        string category,
        string message,
        DateTimeOffset occurredAt,
        IReadOnlyList<LogField>? fields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Level = level;
        Category = category;
        Message = message;
        OccurredAt = occurredAt;
        Fields = new ReadOnlyCollection<LogField>((fields ?? Array.Empty<LogField>()).ToArray());
    }

    public LogLevel Level { get; }

    public string Category { get; }

    public string Message { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyList<LogField> Fields { get; }
}

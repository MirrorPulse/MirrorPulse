namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Normalized BCP-47-style locale used by MP and Adapter resources.
/// </summary>
public readonly record struct LocaleCode
{
    public LocaleCode(string value)
    {
        if (!TryNormalize(value, out var normalized))
        {
            throw new ArgumentException("Locale codes must contain language and optional region segments.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public static LocaleCode EnglishUnitedStates => new("en-US");

    public static LocaleCode Parse(string value) => new(value);

    public static bool TryParse(string? value, out LocaleCode locale)
    {
        if (TryNormalize(value, out var normalized))
        {
            locale = new LocaleCode(normalized);
            return true;
        }

        locale = default;
        return false;
    }

    public override string ToString() => Value;

    private static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value != value.Trim())
        {
            return false;
        }

        var segments = value.Split('-');
        if (segments.Length is < 1 or > 3 || segments.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        if (segments[0].Length is < 2 or > 8 || !segments[0].All(IsAsciiLetter))
        {
            return false;
        }

        if (segments.Skip(1).Any(segment => segment.Length is < 2 or > 8 || !segment.All(IsAsciiLetterOrDigit)))
        {
            return false;
        }

        normalized = string.Join('-', segments.Select((segment, index) => index == 0
            ? segment.ToLowerInvariant()
            : segment.Length == 2 || segment.Length == 3
                ? segment.ToUpperInvariant()
                : char.ToUpperInvariant(segment[0]) + segment[1..].ToLowerInvariant()));
        return true;
    }

    private static bool IsAsciiLetter(char value) => value is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsAsciiLetterOrDigit(char value) => IsAsciiLetter(value) || value is (>= '0' and <= '9');
}

/// <summary>
/// Stable key for a localized UI or Adapter resource.
/// </summary>
public readonly record struct LocalizedResourceKey
{
    public LocalizedResourceKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value != value.Trim() ||
            value.Any(character => !(char.IsLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new ArgumentException("Localized resource keys must be short dot-separated identifiers.", nameof(value));
        }

        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

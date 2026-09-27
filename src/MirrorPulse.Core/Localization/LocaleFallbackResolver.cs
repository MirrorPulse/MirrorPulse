using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Localization;

public static class LocaleFallbackResolver
{
    public static LocaleCode Resolve(
        LocaleCode requested,
        IEnumerable<LocaleCode> availableLocales,
        LocaleCode fallback)
    {
        ArgumentNullException.ThrowIfNull(availableLocales);
        var available = availableLocales.Distinct().ToArray();
        if (available.Length == 0)
        {
            throw new ArgumentException("At least one locale must be available.", nameof(availableLocales));
        }

        var exact = available.FirstOrDefault(locale => string.Equals(locale.Value, requested.Value, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exact.Value))
        {
            return exact;
        }

        var language = requested.Value.Split('-')[0];
        var languageMatch = available.FirstOrDefault(locale => string.Equals(locale.Value.Split('-')[0], language, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(languageMatch.Value))
        {
            return languageMatch;
        }

        var fallbackMatch = available.FirstOrDefault(locale => string.Equals(locale.Value, fallback.Value, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(fallbackMatch.Value))
        {
            throw new InvalidOperationException("The configured fallback locale is not available.");
        }

        return fallbackMatch;
    }
}

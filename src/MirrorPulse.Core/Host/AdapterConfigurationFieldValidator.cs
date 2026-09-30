using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

/// <summary>Applies declared defaults and validates each instance before it reaches a Worker.</summary>
public static class AdapterConfigurationFieldValidator
{
    public static IReadOnlyDictionary<string, string> ValidateAndApplyDefaults(
        AdapterManifest manifest,
        IReadOnlyDictionary<string, string> values,
        string? secret)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(values);
        var result = new Dictionary<string, string>(values, StringComparer.Ordinal);
        if (result.ContainsKey("credentialReference"))
        {
            throw new InvalidDataException("Credential references are managed by MirrorPulse.");
        }

        foreach (AdapterConfigurationField field in manifest.ConfigurationFields)
        {
            if (field.Kind == AdapterConfigurationFieldKind.Secret)
            {
                if (field.Required && string.IsNullOrEmpty(secret))
                {
                    throw new InvalidDataException($"The Adapter requires '{field.Label}'.");
                }

                continue;
            }

            if (!result.TryGetValue(field.Key, out string? value) && field.DefaultValue is not null)
            {
                value = field.DefaultValue;
                result[field.Key] = value;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.Required)
                {
                    throw new InvalidDataException($"The Adapter requires '{field.Label}'.");
                }

                result.Remove(field.Key);
                continue;
            }

            if (value.Length > 4096 ||
                (field.Kind == AdapterConfigurationFieldKind.Choice &&
                    !field.Options.Contains(value, StringComparer.Ordinal)) ||
                (field.Kind == AdapterConfigurationFieldKind.Toggle &&
                    !bool.TryParse(value, out _)))
            {
                throw new InvalidDataException($"The Adapter setting '{field.Label}' is invalid.");
            }

            if (field.Kind == AdapterConfigurationFieldKind.Toggle)
            {
                result[field.Key] = bool.Parse(value) ? "true" : "false";
            }
        }

        return result;
    }
}

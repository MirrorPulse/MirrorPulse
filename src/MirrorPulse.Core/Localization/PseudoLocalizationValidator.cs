using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Localization;

public sealed record PseudoLocalizationIssue(string Key, string Message);

public sealed record PseudoLocalizationReport(IReadOnlyList<PseudoLocalizationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public static class PseudoLocalizationValidator
{
    public static PseudoLocalizationReport Validate(IEnumerable<KeyValuePair<string, string>> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        var issues = new List<PseudoLocalizationIssue>();
        foreach (var resource in resources)
        {
            try
            {
                _ = new LocalizedResourceKey(resource.Key);
            }
            catch (ArgumentException)
            {
                issues.Add(new PseudoLocalizationIssue(resource.Key, "The resource key is invalid."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(resource.Value))
            {
                issues.Add(new PseudoLocalizationIssue(resource.Key, "The resource value is empty."));
            }
        }

        return new PseudoLocalizationReport(new ReadOnlyCollection<PseudoLocalizationIssue>(issues));
    }

    public static string Expand(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var builder = new System.Text.StringBuilder(value.Length * 2 + 2);
        builder.Append('［');
        foreach (var character in value)
        {
            builder.Append(character is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') ? ToFullWidth(character) : character);
        }

        builder.Append('］');
        return builder.ToString();
    }

    private static char ToFullWidth(char character) => (char)(character + 0xFEE0);
}

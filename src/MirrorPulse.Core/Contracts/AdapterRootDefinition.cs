namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Adapter-declared metadata for one first-level MirrorPulse directory.
/// </summary>
public sealed record AdapterRootDefinition
{
    public AdapterRootDefinition(string key, string label, string directoryName, bool customEntry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!IsSafeDirectoryName(directoryName))
        {
            throw new ArgumentException("Root definition directory names must be one safe path segment.", nameof(directoryName));
        }

        Key = key;
        Label = label;
        DirectoryName = directoryName;
        CustomEntry = customEntry;
    }

    public string Key { get; }

    public string Label { get; }

    public string DirectoryName { get; }

    public bool CustomEntry { get; }

    private static bool IsSafeDirectoryName(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value == value.TrimEnd(' ', '.') &&
        !value.Contains('/') && !value.Contains('\\') && value is not ("." or "..") &&
        !Path.GetInvalidFileNameChars().Any(value.Contains);
}

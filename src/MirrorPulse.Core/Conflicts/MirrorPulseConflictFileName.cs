using System.Globalization;

namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Creates deterministic, collision-resistant names for preserved conflict copies.
/// </summary>
public static class MirrorPulseConflictFileName
{
    public static string Create(MirrorPulseConflictRecord conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        var sourceName = Path.GetFileName(conflict.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        var safeName = Sanitize(sourceName);
        var timestamp = conflict.DetectedAt.UtcDateTime.ToString(
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture);
        return $"{timestamp}-{conflict.ConflictId:N}-{safeName}";
    }

    private static string Sanitize(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return "item";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var characters = sourceName.Select(character =>
            character is '\0' || char.IsControl(character) || invalid.Contains(character)
                ? '_'
                : character).ToArray();
        var sanitized = new string(characters).Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(sanitized) ? "item" : sanitized;
    }
}

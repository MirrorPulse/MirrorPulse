namespace MirrorPulse.Core.Contracts;

public static class SourceDirectoryPathNormalizer
{
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!System.IO.Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("A source directory must be an absolute local path.", nameof(path));
        }

        var normalized = System.IO.Path.GetFullPath(path.Trim());
        var root = System.IO.Path.GetPathRoot(normalized);
        if (root is not null && normalized.Length > root.Length)
        {
            normalized = normalized.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        }

        return normalized;
    }

    public static bool IsWithin(string rootPath, string candidatePath)
    {
        var root = Normalize(rootPath);
        var candidate = Normalize(candidatePath);
        if (string.Equals(root, candidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = root.EndsWith(System.IO.Path.DirectorySeparatorChar) || root.EndsWith(System.IO.Path.AltDirectorySeparatorChar)
            ? root
            : root + System.IO.Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}

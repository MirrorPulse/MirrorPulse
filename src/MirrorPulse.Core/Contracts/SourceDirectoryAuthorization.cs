using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

[Flags]
public enum SourceDirectoryAccess
{
    None = 0,
    Read = 1,
    Write = 2,
    Enumerate = 4,
}

/// <summary>
/// One absolute local directory permission granted to a Worker.
/// </summary>
public sealed record SourceDirectoryGrant
{
    public SourceDirectoryGrant(string path, SourceDirectoryAccess access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!System.IO.Path.IsPathRooted(path))
        {
            throw new ArgumentException("An authorized source directory must be an absolute path.", nameof(path));
        }

        if (access == SourceDirectoryAccess.None)
        {
            throw new ArgumentException("A source directory grant must include at least one access right.", nameof(access));
        }

        Path = System.IO.Path.GetFullPath(path);
        Access = access;
    }

    public string Path { get; }

    public SourceDirectoryAccess Access { get; }
}

/// <summary>
/// Immutable per-instance source directory authorization list sent during startup.
/// </summary>
public sealed record SourceDirectoryAuthorizationList
{
    public SourceDirectoryAuthorizationList(InstanceId instanceId, IEnumerable<SourceDirectoryGrant> grants)
    {
        ArgumentNullException.ThrowIfNull(grants);
        var copied = grants.ToArray();
        if (copied.GroupBy(grant => grant.Path, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("A Worker authorization list cannot contain duplicate directories.", nameof(grants));
        }

        InstanceId = instanceId;
        Grants = new ReadOnlyCollection<SourceDirectoryGrant>(copied);
    }

    public InstanceId InstanceId { get; }

    public IReadOnlyList<SourceDirectoryGrant> Grants { get; }

    public bool Allows(string path, SourceDirectoryAccess access)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var normalized = System.IO.Path.GetFullPath(path);
        return Grants.Any(grant => string.Equals(grant.Path, normalized, StringComparison.OrdinalIgnoreCase) && (grant.Access & access) == access);
    }
}

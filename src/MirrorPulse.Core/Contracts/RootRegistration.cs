namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Registration state of an Adapter-controlled first-level Cloud Files directory.
/// </summary>
public enum RootRegistrationState
{
    Pending,
    Active,
    Disabled,
    Conflicted,
    Removed
}

/// <summary>
/// One first-level directory contributed by an Adapter Instance.
/// </summary>
public sealed record RootRegistration
{
    public RootRegistration(
        AdapterId adapterId,
        InstanceId instanceId,
        RootId rootId,
        string uniquenessKey,
        string label,
        string directoryName,
        bool customEntry,
        RootRegistrationState state,
        DateTimeOffset registeredAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uniquenessKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!IsSafeDirectoryName(directoryName))
        {
            throw new ArgumentException("A root directory name must be one safe first-level segment.", nameof(directoryName));
        }

        AdapterId = adapterId;
        InstanceId = instanceId;
        RootId = rootId;
        UniquenessKey = uniquenessKey;
        Label = label;
        DirectoryName = directoryName;
        CustomEntry = customEntry;
        State = state;
        RegisteredAt = registeredAt;
    }

    public AdapterId AdapterId { get; }

    public InstanceId InstanceId { get; }

    public RootId RootId { get; }

    /// <summary>
    /// Adapter-provided key used by MP to reject global root-name collisions.
    /// </summary>
    public string UniquenessKey { get; }

    public string Label { get; }

    public string DirectoryName { get; }

    public bool CustomEntry { get; }

    public RootRegistrationState State { get; }

    public DateTimeOffset RegisteredAt { get; }

    private static bool IsSafeDirectoryName(string? directoryName)
    {
        if (string.IsNullOrWhiteSpace(directoryName) || directoryName.Contains('/') || directoryName.Contains('\\'))
        {
            return false;
        }

        return directoryName is not ("." or "..") &&
            directoryName == directoryName.TrimEnd(' ', '.') &&
            !Path.GetInvalidFileNameChars().Any(directoryName.Contains);
    }
}

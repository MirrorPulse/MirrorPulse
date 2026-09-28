using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.Core.CloudFiles;

public enum MirrorPulseRemoteOperationKind
{
    CreateFile,
    UpdateFile,
    Move,
    Delete,
    MetadataUpdate,
}

/// <summary>
/// Normalized remote change information retained by MP before native application.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed record MirrorPulseRemoteOperation
{
    public MirrorPulseRemoteOperation(
        MirrorPulseRemoteOperationKind kind,
        string changeId,
        string remoteId,
        string remoteRevision,
        string relativePath,
        CloudItemKind itemKind,
        Guid? itemId,
        string? previousRemoteRevision,
        string? previousRelativePath,
        long? length,
        CloudPlaceholderMetadata? metadata,
        ReadOnlyMemory<byte> cursorAfter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(changeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteId);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteRevision);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (kind == MirrorPulseRemoteOperationKind.Move)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(previousRelativePath);
        }

        Kind = kind;
        ChangeId = changeId;
        RemoteId = remoteId;
        RemoteRevision = remoteRevision;
        RelativePath = relativePath;
        ItemKind = itemKind;
        ItemId = itemId;
        PreviousRemoteRevision = previousRemoteRevision;
        PreviousRelativePath = previousRelativePath;
        Length = length;
        Metadata = metadata;
        CursorAfter = cursorAfter.ToArray();
    }

    public MirrorPulseRemoteOperationKind Kind { get; }

    public string ChangeId { get; }

    public string RemoteId { get; }

    public string RemoteRevision { get; }

    public string RelativePath { get; }

    public CloudItemKind ItemKind { get; }

    public Guid? ItemId { get; }

    public string? PreviousRemoteRevision { get; }

    public string? PreviousRelativePath { get; }

    public long? Length { get; }

    public CloudPlaceholderMetadata? Metadata { get; }

    public ReadOnlyMemory<byte> CursorAfter { get; }
}

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteOperationFactory
{
    public static MirrorPulseRemoteOperation Create(
        CloudRemoteChange change,
        MirrorPulseRemoteOperationKind kind) => new(
            kind,
            change.ChangeId,
            change.RemoteId,
            change.RemoteRevision,
            change.RelativePath,
            change.ItemKind,
            change.ItemId,
            change.PreviousRemoteRevision,
            change.PreviousRelativePath,
            change.Length,
            change.Metadata,
            change.CursorAfter);
}

[SupportedOSPlatform("windows10.0.16299")]
public static class MirrorPulseRemoteCreateMapper
{
    public static MirrorPulseRemoteOperation Map(CloudRemoteChange change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (change.Kind != CloudRemoteChangeKind.FileUpsert
            || change.PreviousRemoteRevision is not null
            || change.PreviousRelativePath is not null)
        {
            throw new InvalidDataException("The remote change is not a file creation.");
        }

        return MirrorPulseRemoteOperationFactory.Create(change, MirrorPulseRemoteOperationKind.CreateFile);
    }
}

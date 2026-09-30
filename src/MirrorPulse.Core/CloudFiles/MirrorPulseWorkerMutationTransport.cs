using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>Carries one local deletion from the CfSharp journal to an Adapter Worker.</summary>
public sealed record MirrorPulseWorkerDeleteRequest(
    InstanceId InstanceId,
    string NormalizedPath,
    string? ExpectedRevision,
    bool IsDirectory);

/// <summary>Carries one local move from the CfSharp journal to an Adapter Worker.</summary>
public sealed record MirrorPulseWorkerMoveRequest(
    InstanceId InstanceId,
    string SourcePath,
    string DestinationPath,
    string? ExpectedRevision,
    bool IsDirectory);

public interface IMirrorPulseWorkerMutationTransport
{
    ValueTask<string?> DeleteAsync(
        MirrorPulseWorkerDeleteRequest request,
        CancellationToken cancellationToken);

    ValueTask<string> MoveAsync(
        MirrorPulseWorkerMoveRequest request,
        CancellationToken cancellationToken);
}

public sealed class MirrorPulseWorkerMutationConflictException : IOException
{
    public MirrorPulseWorkerMutationConflictException(
        string? expectedRevision,
        string? actualRevision)
        : base("The Adapter Worker rejected the mutation because the remote item changed.")
    {
        ExpectedRevision = expectedRevision;
        ActualRevision = actualRevision;
    }

    public string? ExpectedRevision { get; }

    public string? ActualRevision { get; }
}

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Worker hello data used to negotiate a common protocol version.
/// </summary>
public sealed record WorkerProtocolOffer
{
    public WorkerProtocolOffer(
        AdapterId adapterId,
        string adapterVersion,
        WorkerSessionId workerSessionId,
        string runtimeIdentifier,
        ProtocolVersionRange supportedVersions,
        Sha256Digest manifestSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeIdentifier);
        AdapterId = adapterId;
        AdapterVersion = adapterVersion;
        WorkerSessionId = workerSessionId;
        RuntimeIdentifier = runtimeIdentifier;
        SupportedVersions = supportedVersions;
        ManifestSha256 = manifestSha256;
    }

    public AdapterId AdapterId { get; }

    public string AdapterVersion { get; }

    public WorkerSessionId WorkerSessionId { get; }

    public string RuntimeIdentifier { get; }

    public ProtocolVersionRange SupportedVersions { get; }

    public Sha256Digest ManifestSha256 { get; }
}

/// <summary>
/// MP's accepted or rejected protocol version selection.
/// </summary>
public sealed record WorkerProtocolSelection
{
    public WorkerProtocolSelection(bool accepted, int? selectedVersion, string? rejectionCode = null)
    {
        if (accepted && selectedVersion is null)
        {
            throw new ArgumentException("An accepted negotiation must select a protocol version.", nameof(selectedVersion));
        }

        if (!accepted && string.IsNullOrWhiteSpace(rejectionCode))
        {
            throw new ArgumentException("A rejected negotiation must include a reason.", nameof(rejectionCode));
        }

        Accepted = accepted;
        SelectedVersion = selectedVersion;
        RejectionCode = rejectionCode;
    }

    public bool Accepted { get; }

    public int? SelectedVersion { get; }

    public string? RejectionCode { get; }
}

using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// One user-configured connection created from an installed Adapter.
/// </summary>
public sealed record AdapterInstance
{
    public AdapterInstance(
        AdapterId adapterId,
        InstallId installId,
        InstanceId instanceId,
        string displayName,
        IReadOnlyDictionary<string, string> configuration,
        IReadOnlyList<string> credentialReferences,
        string fileCacheDirectory,
        string transferCacheDirectory,
        bool enabled,
        AdapterLifecycleState lifecycleState,
        WorkerSessionId? workerSessionId,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentialReferences);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileCacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(transferCacheDirectory);
        AdapterId = adapterId;
        InstallId = installId;
        InstanceId = instanceId;
        DisplayName = displayName;
        Configuration = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(configuration, StringComparer.Ordinal));
        CredentialReferences = new ReadOnlyCollection<string>(credentialReferences.ToArray());
        FileCacheDirectory = fileCacheDirectory;
        TransferCacheDirectory = transferCacheDirectory;
        Enabled = enabled;
        LifecycleState = lifecycleState;
        WorkerSessionId = workerSessionId;
        CreatedAt = createdAt;
    }

    public AdapterId AdapterId { get; }

    public InstallId InstallId { get; }

    public InstanceId InstanceId { get; }

    public string DisplayName { get; }

    /// <summary>
    /// Non-secret configuration owned by MP and supplied to the Worker at startup.
    /// </summary>
    public IReadOnlyDictionary<string, string> Configuration { get; }

    /// <summary>
    /// References to secrets in the MP-managed credential store, never secret values.
    /// </summary>
    public IReadOnlyList<string> CredentialReferences { get; }

    public string FileCacheDirectory { get; }

    public string TransferCacheDirectory { get; }

    public bool Enabled { get; }

    public AdapterLifecycleState LifecycleState { get; }

    public WorkerSessionId? WorkerSessionId { get; }

    public DateTimeOffset CreatedAt { get; }
}

using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Network permission granted to a Worker after MP evaluates Adapter capabilities.
/// </summary>
public sealed record NetworkAccessPolicy
{
    public NetworkAccessPolicy(bool allowed, IEnumerable<string>? allowedHosts = null)
    {
        var hosts = (allowedHosts ?? Array.Empty<string>()).Select(NormalizeHost).ToArray();
        if (hosts.Distinct(StringComparer.OrdinalIgnoreCase).Count() != hosts.Length)
        {
            throw new ArgumentException("Network hosts must be unique.", nameof(allowedHosts));
        }

        if (!allowed && hosts.Length > 0)
        {
            throw new ArgumentException("A denied network policy cannot include allowed hosts.", nameof(allowedHosts));
        }

        Allowed = allowed;
        AllowedHosts = new ReadOnlyCollection<string>(hosts);
    }

    public bool Allowed { get; }

    public IReadOnlyList<string> AllowedHosts { get; }

    public bool Allows(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        return Allowed && (AllowedHosts.Count == 0 || AllowedHosts.Contains(NormalizeHost(endpoint.Host), StringComparer.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        var normalized = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (Uri.CheckHostName(normalized) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("Network host names must be valid DNS names or IP addresses.", nameof(host));
        }

        return normalized;
    }
}

/// <summary>
/// Effective capabilities sent to one Worker instance.
/// </summary>
public sealed record WorkerCapabilityGrant
{
    public WorkerCapabilityGrant(
        InstanceId instanceId,
        AdapterCapabilities declaredCapabilities,
        NetworkAccessPolicy network)
    {
        ArgumentNullException.ThrowIfNull(declaredCapabilities);
        ArgumentNullException.ThrowIfNull(network);
        if (!declaredCapabilities.Network && network.Allowed)
        {
            throw new ArgumentException("A Worker cannot receive network access that its Adapter did not declare.", nameof(network));
        }

        InstanceId = instanceId;
        DeclaredCapabilities = declaredCapabilities;
        Network = network;
    }

    public InstanceId InstanceId { get; }

    public AdapterCapabilities DeclaredCapabilities { get; }

    public NetworkAccessPolicy Network { get; }
}

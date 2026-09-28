using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Adapters.Smb;

public sealed record MirrorPulseSmbCredentialBinding(
    string NetworkPath,
    string? AccountName,
    bool UsesCurrentWindowsIdentity,
    CredentialReference? Reference);

/// <summary>
/// Resolves optional SMB credentials through MP's current-user, instance-scoped broker.
/// </summary>
public sealed class MirrorPulseSmbCredentialProvider
{
    private readonly InstanceId _instanceId;
    private readonly ICredentialBroker _broker;

    public MirrorPulseSmbCredentialProvider(InstanceId instanceId, ICredentialBroker broker)
    {
        ArgumentNullException.ThrowIfNull(broker);
        _instanceId = instanceId;
        _broker = broker;
    }

    public static MirrorPulseSmbCredentialBinding UseCurrentIdentity(string networkPath)
    {
        ValidateNetworkPath(networkPath);
        return new(networkPath, null, true, null);
    }

    public async ValueTask<(MirrorPulseSmbCredentialBinding Binding, CredentialLease Lease)> ResolveAsync(
        string networkPath,
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkPath(networkPath);
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Scope != CredentialScope.CurrentUser ||
            reference.Kind is not (CredentialKind.WindowsCredential or CredentialKind.Password))
        {
            throw new ArgumentException("SMB requires a current-user Windows or password credential.", nameof(reference));
        }

        var lease = await _broker.ResolveAsync(
            new CredentialBrokerRequest(_instanceId, reference, "smb.connect"),
            cancellationToken).ConfigureAwait(false);
        return (new MirrorPulseSmbCredentialBinding(networkPath, reference.AccountHint, false, reference), lease);
    }

    private static void ValidateNetworkPath(string networkPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(networkPath);
        if (!networkPath.StartsWith("\\\\", StringComparison.Ordinal))
        {
            throw new ArgumentException("SMB credentials require a UNC path.", nameof(networkPath));
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using CfSharp;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.CloudFiles;

/// <summary>
/// Maps an Adapter instance and remote object identity to a stable CfSharp placeholder identity.
/// </summary>
public sealed record MirrorPulsePlaceholderIdentity(
    InstanceId InstanceId,
    string RemoteId,
    string? RemoteRevision)
{
    public CloudPlaceholderIdentity ToCfSharp()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RemoteId);
        return new(CreateItemId(InstanceId, RemoteId), RemoteId.Trim(), RemoteRevision?.Trim() ?? string.Empty);
    }

    public byte[] Encode() => ToCfSharp().Encode();

    public static MirrorPulsePlaceholderIdentity Create(InstanceId instanceId, string remoteId, string? remoteRevision = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteId);
        return new(instanceId, remoteId.Trim(), remoteRevision?.Trim());
    }

    public static MirrorPulsePlaceholderIdentity Decode(InstanceId instanceId, ReadOnlySpan<byte> encoded)
    {
        var identity = CloudPlaceholderIdentity.Decode(encoded);
        return new(instanceId, identity.RemoteId, identity.RemoteRevision);
    }

    private static Guid CreateItemId(InstanceId instanceId, string remoteId)
    {
        var input = Encoding.UTF8.GetBytes($"MirrorPulse/{instanceId}/{remoteId.Trim()}");
        var digest = SHA256.HashData(input);
        var itemId = new Guid(digest.AsSpan(0, 16));
        return itemId == Guid.Empty ? new Guid(digest.AsSpan(16, 16)) : itemId;
    }
}

using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.State;

public sealed record MirrorPulseRecoveryDecision(
    InstanceId InstanceId,
    string Phase,
    bool RequiresFullRescan,
    ReadOnlyMemory<byte> RemoteCursor,
    DateTimeOffset SavedAt);

/// <summary>
/// Persists the last sync phase so a restarted Host can resume or force a full rescan deliberately.
/// </summary>
public sealed class MirrorPulseRestartRecoveryCoordinator
{
    public const string KeyPrefix = "mirrorpulse/recovery/v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ICfSharpStateStore _store;

    public MirrorPulseRestartRecoveryCoordinator(ICfSharpStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask SaveAsync(
        InstanceId instanceId,
        string phase,
        bool requiresFullRescan,
        ReadOnlyMemory<byte> remoteCursor,
        DateTimeOffset savedAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);
        var document = new RecoveryDocument(
            instanceId.ToString(),
            phase.Trim(),
            requiresFullRescan,
            remoteCursor.ToArray(),
            savedAt);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        await _store.WriteAsync(GetKey(instanceId), bytes, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<MirrorPulseRecoveryDecision?> RestoreAsync(
        InstanceId instanceId,
        CancellationToken cancellationToken = default)
    {
        var value = await _store.ReadAsync(GetKey(instanceId), cancellationToken).ConfigureAwait(false);
        if (value is null)
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<RecoveryDocument>(value.Value.Span, SerializerOptions)
                ?? throw new InvalidDataException("The recovery checkpoint is empty.");
            if (!string.Equals(document.InstanceId, instanceId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("The recovery checkpoint belongs to another instance.");
            }

            ArgumentException.ThrowIfNullOrWhiteSpace(document.Phase);
            return new(
                instanceId,
                document.Phase.Trim(),
                document.RequiresFullRescan,
                document.RemoteCursor.ToArray(),
                document.SavedAt);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The recovery checkpoint is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The recovery checkpoint contains invalid values.", exception);
        }
    }

    public static string GetKey(InstanceId instanceId) => $"{KeyPrefix}/{instanceId}";

    private sealed record RecoveryDocument(
        string InstanceId,
        string Phase,
        bool RequiresFullRescan,
        byte[] RemoteCursor,
        DateTimeOffset SavedAt);
}

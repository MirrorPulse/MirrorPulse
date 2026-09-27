using System.Text.Json;
using MirrorPulse.Core.State;

namespace MirrorPulse.Core.CloudFiles;

public sealed record MirrorPulseSyncRootRegistrationState(
    string Path,
    Guid ProviderId,
    string ProviderVersion,
    DateTimeOffset RegisteredAt);

/// <summary>
/// Persists the exact root path needed to reopen the durable Windows registration.
/// </summary>
public sealed class MirrorPulseSyncRootRegistrationStateStore
{
    public const string DefaultKey = "mirrorpulse/sync-root-registration/v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ICfSharpStateStore _store;
    private readonly string _key;

    public MirrorPulseSyncRootRegistrationStateStore(ICfSharpStateStore store, string key = DefaultKey)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store = store;
        _key = key.Trim();
    }

    public async Task SaveAsync(MirrorPulseSyncRootRegistrationState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, SerializerOptions);
        await _store.WriteAsync(_key, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MirrorPulseSyncRootRegistrationState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await _store.ReadAsync(_key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<MirrorPulseSyncRootRegistrationState>(bytes.Value.Span, SerializerOptions)
                ?? throw new InvalidDataException("The sync-root registration state is empty.");
            ArgumentException.ThrowIfNullOrWhiteSpace(state.Path);
            ArgumentException.ThrowIfNullOrWhiteSpace(state.ProviderVersion);
            if (state.ProviderId == Guid.Empty)
            {
                throw new ArgumentException("The sync-root registration provider ID is empty.");
            }

            return state with { Path = System.IO.Path.GetFullPath(state.Path.Trim()), ProviderVersion = state.ProviderVersion.Trim() };
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The sync-root registration state is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The sync-root registration state contains invalid values.", exception);
        }
    }
}

using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.State;

/// <summary>
/// Minimal boundary implemented by the CfSharp preview-backed state store.
/// </summary>
public interface ICfSharpStateStore
{
    ValueTask<ReadOnlyMemory<byte>?> ReadAsync(string key, CancellationToken cancellationToken = default);

    ValueTask WriteAsync(string key, ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Serializes MP persistent state through a CfSharp state document without coupling Core to its package API.
/// </summary>
public sealed class MirrorPulsePersistentStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public const string DefaultKey = "mirrorpulse/persistent-state/v1";

    private readonly ICfSharpStateStore _store;
    private readonly string _key;

    public MirrorPulsePersistentStateStore(ICfSharpStateStore store, string key = DefaultKey)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _store = store;
        _key = key.Trim();
    }

    public async Task SaveAsync(MirrorPulsePersistentState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var entries = state.Instances.Select(pair => new InstanceDocument(
            pair.Key.ToString(),
            pair.Value.LocalChangeCursor,
            pair.Value.RemoteChangeCursor,
            pair.Value.LocalSequence,
            pair.Value.PendingOperationCount,
            pair.Value.ConflictCount,
            pair.Value.LastSuccessfulSync)).ToArray();
        var document = new StateDocument(state.SchemaVersion, state.SavedAt, entries);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
        await _store.WriteAsync(_key, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MirrorPulsePersistentState?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var bytes = await _store.ReadAsync(_key, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<StateDocument>(bytes.Value.Span, SerializerOptions)
                ?? throw new InvalidDataException("The persistent state document is empty.");
            var instances = document.Instances.ToDictionary(
                entry => InstanceId.Parse(entry.InstanceId),
                entry => new InstancePersistentState(
                    entry.LocalChangeCursor,
                    entry.RemoteChangeCursor,
                    entry.LocalSequence,
                    entry.PendingOperationCount,
                    entry.ConflictCount,
                    entry.LastSuccessfulSync));
            return new MirrorPulsePersistentState(document.SchemaVersion, document.SavedAt, instances);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The persistent state document is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The persistent state document contains invalid values.", exception);
        }
    }

    private sealed record StateDocument(int SchemaVersion, DateTimeOffset SavedAt, InstanceDocument[] Instances);

    private sealed record InstanceDocument(
        string InstanceId,
        string? LocalChangeCursor,
        string? RemoteChangeCursor,
        long LocalSequence,
        int PendingOperationCount,
        int ConflictCount,
        DateTimeOffset? LastSuccessfulSync);
}

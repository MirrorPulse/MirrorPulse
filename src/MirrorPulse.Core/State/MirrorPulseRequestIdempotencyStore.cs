using System.Text.Json;

namespace MirrorPulse.Core.State;

public sealed record MirrorPulseIdempotencyRecord(
    Guid RequestId,
    string Fingerprint,
    DateTimeOffset RecordedAt);

/// <summary>
/// Stores request fingerprints so retried Worker commands cannot apply twice with different payloads.
/// </summary>
public sealed class MirrorPulseRequestIdempotencyStore : IDisposable
{
    public const string KeyPrefix = "mirrorpulse/idempotency/v1";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ICfSharpStateStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public MirrorPulseRequestIdempotencyStore(ICfSharpStateStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    public async ValueTask<MirrorPulseIdempotencyRecord?> ReadAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        ValidateRequestId(requestId);
        var value = await _store.ReadAsync(GetKey(requestId), cancellationToken).ConfigureAwait(false);
        return value is null ? null : Deserialize(value.Value.Span, requestId);
    }

    public async ValueTask<bool> TryRecordAsync(
        Guid requestId,
        string fingerprint,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken = default)
    {
        ValidateRequestId(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fingerprint);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _store.ReadAsync(GetKey(requestId), cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                var record = Deserialize(existing.Value.Span, requestId);
                if (!string.Equals(record.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A request ID was recorded with a different fingerprint.");
                }

                return false;
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(
                new MirrorPulseIdempotencyRecord(requestId, fingerprint, recordedAt),
                SerializerOptions);
            await _store.WriteAsync(GetKey(requestId), bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public static string GetKey(Guid requestId)
    {
        ValidateRequestId(requestId);
        return $"{KeyPrefix}/{requestId:D}";
    }

    public void Dispose() => _gate.Dispose();

    private static MirrorPulseIdempotencyRecord Deserialize(ReadOnlySpan<byte> bytes, Guid requestId)
    {
        try
        {
            var record = JsonSerializer.Deserialize<MirrorPulseIdempotencyRecord>(bytes, SerializerOptions)
                ?? throw new InvalidDataException("The idempotency record is empty.");
            if (record.RequestId != requestId || string.IsNullOrWhiteSpace(record.Fingerprint))
            {
                throw new InvalidDataException("The idempotency record contains invalid identity data.");
            }

            return record;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The idempotency record is not valid JSON.", exception);
        }
    }

    private static void ValidateRequestId(Guid requestId)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A request ID cannot be empty.", nameof(requestId));
        }
    }
}

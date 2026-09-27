using System.Text.Json;
using System.Text.Json.Serialization;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Serializes and deserializes the UTF-8 JSON envelope used by control frames.
/// </summary>
public static class ControlFrameJsonCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static byte[] Encode(ControlFrameEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var dto = new ControlFrameDto(
            envelope.ProtocolVersion,
            envelope.MessageType,
            envelope.RequestId,
            envelope.InstanceId.ToString(),
            envelope.WorkerSessionId.ToString(),
            envelope.IsResponse,
            envelope.Payload);
        return JsonSerializer.SerializeToUtf8Bytes(dto, SerializerOptions);
    }

    public static ControlFrameEnvelope Decode(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            throw new JsonException("A control frame cannot contain an empty JSON document.");
        }

        var dto = JsonSerializer.Deserialize<ControlFrameDto>(utf8Json, SerializerOptions)
            ?? throw new JsonException("The control frame JSON document is null.");
        return new ControlFrameEnvelope(
            dto.ProtocolVersion,
            dto.MessageType,
            dto.RequestId,
            InstanceId.Parse(dto.InstanceId),
            WorkerSessionId.Parse(dto.WorkerSessionId),
            dto.IsResponse,
            dto.Payload);
    }

    private sealed record ControlFrameDto(
        int ProtocolVersion,
        string MessageType,
        Guid RequestId,
        string InstanceId,
        string WorkerSessionId,
        bool IsResponse,
        JsonElement Payload);
}

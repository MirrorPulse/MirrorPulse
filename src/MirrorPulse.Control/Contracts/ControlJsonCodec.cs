using System.Text.Json;
using System.Text.Json.Serialization;

namespace MirrorPulse.Control.Contracts;

/// <summary>
/// Canonical JSON codec for control envelopes.
/// </summary>
public static class MirrorPulseControlJsonCodec
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static byte[] Serialize<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var payload = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        if (payload.Length > MirrorPulseControlSchema.MaximumFrameBytes)
        {
            throw new InvalidDataException("The control payload exceeds the protocol limit.");
        }

        return payload;
    }

    public static T Deserialize<T>(ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MirrorPulseControlSchema.MaximumFrameBytes)
        {
            throw new InvalidDataException("The control payload exceeds the protocol limit.");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(payload, SerializerOptions)
                ?? throw new InvalidDataException("The control payload is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The control payload is not valid JSON.", exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            MaxDepth = 64
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

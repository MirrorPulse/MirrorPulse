using System.Buffers.Binary;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Length-prefix and payload limits for Named Pipe frames.
/// </summary>
public static class ControlFrameLimits
{
    public const int LengthPrefixBytes = 4;
    public const uint MaxPayloadBytes = 4 * 1024 * 1024;

    public static long GetFrameLength(uint payloadLength)
    {
        ValidatePayloadLength(payloadLength);
        return LengthPrefixBytes + payloadLength;
    }

    public static void ValidatePayloadLength(uint payloadLength)
    {
        if (payloadLength > MaxPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadLength), "The control frame payload exceeds the protocol limit.");
        }
    }

    public static uint ReadPayloadLength(ReadOnlySpan<byte> lengthPrefix)
    {
        if (lengthPrefix.Length != LengthPrefixBytes)
        {
            throw new ArgumentException("A frame length prefix must contain exactly four bytes.", nameof(lengthPrefix));
        }

        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(lengthPrefix);
        ValidatePayloadLength(payloadLength);
        return payloadLength;
    }

    public static void WritePayloadLength(uint payloadLength, Span<byte> lengthPrefix)
    {
        ValidatePayloadLength(payloadLength);
        if (lengthPrefix.Length != LengthPrefixBytes)
        {
            throw new ArgumentException("A frame length prefix must contain exactly four bytes.", nameof(lengthPrefix));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(lengthPrefix, payloadLength);
    }
}

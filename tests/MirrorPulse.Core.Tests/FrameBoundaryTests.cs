using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class FrameBoundaryTests
{
    [TestMethod]
    public void FrameLengthUsesFourByteLittleEndianPrefix()
    {
        Span<byte> prefix = stackalloc byte[ControlFrameLimits.LengthPrefixBytes];
        ControlFrameLimits.WritePayloadLength(0x00123456, prefix);

        Assert.AreEqual((byte)0x56, prefix[0]);
        Assert.AreEqual((byte)0x34, prefix[1]);
        Assert.AreEqual((uint)0x00123456, ControlFrameLimits.ReadPayloadLength(prefix));
        Assert.AreEqual(0x0012345AL, ControlFrameLimits.GetFrameLength(0x00123456));
    }

    [TestMethod]
    public void FrameBoundaryRejectsOversizedOrMalformedPrefixes()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            ControlFrameLimits.ValidatePayloadLength(ControlFrameLimits.MaxPayloadBytes + 1));
        Assert.ThrowsExactly<ArgumentException>(() =>
            ControlFrameLimits.ReadPayloadLength(new byte[3]));
    }
}

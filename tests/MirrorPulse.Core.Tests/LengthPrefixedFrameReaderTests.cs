using System.Buffers.Binary;
using System.Text;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class LengthPrefixedFrameReaderTests
{
    [TestMethod]
    public async Task ReaderHandlesFragmentedPrefixAndPayload()
    {
        const string expected = "worker-ready";
        var payload = Encoding.UTF8.GetBytes(expected);
        var frame = new byte[ControlFrameLimits.LengthPrefixBytes + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, (uint)payload.Length);
        payload.CopyTo(frame.AsSpan(ControlFrameLimits.LengthPrefixBytes));

        await using var stream = new FragmentedReadStream(frame, 1);
        var actual = await LengthPrefixedFrameReader.ReadAsync(stream);

        Assert.AreEqual(expected, Encoding.UTF8.GetString(actual));
    }

    [TestMethod]
    public async Task ReaderReturnsEmptyPayload()
    {
        await using var stream = new MemoryStream(new byte[ControlFrameLimits.LengthPrefixBytes]);
        var actual = await LengthPrefixedFrameReader.ReadAsync(stream);

        Assert.HasCount(0, actual);
    }

    [TestMethod]
    public async Task ReaderReportsTruncatedFrame()
    {
        var frame = new byte[ControlFrameLimits.LengthPrefixBytes + 2];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, 3);
        await using var stream = new MemoryStream(frame);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(() => LengthPrefixedFrameReader.ReadAsync(stream).AsTask());
    }

    private sealed class FragmentedReadStream(byte[] data, int maximumRead) : MemoryStream(data)
    {
        private readonly int _maximumRead = maximumRead;

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bounded = buffer[..Math.Min(buffer.Length, _maximumRead)];
            return base.ReadAsync(bounded, cancellationToken);
        }
    }
}

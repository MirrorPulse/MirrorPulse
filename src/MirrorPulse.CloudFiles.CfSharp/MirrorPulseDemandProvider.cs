using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp;

/// <summary>
/// Converts one CfSharp hydration callback into reads from the Worker selected by the stable
/// placeholder identity. The returned seekable stream translates CfSharp absolute offsets into
/// bounded Worker range requests without buffering the entire remote file.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseDemandProvider : ICloudDemandProvider
{
    private readonly InstanceId[] _activeInstances;
    private readonly IMirrorPulseWorkerRangeTransport _transport;

    public MirrorPulseDemandProvider(
        IEnumerable<InstanceId> activeInstances,
        IMirrorPulseWorkerRangeTransport transport)
    {
        ArgumentNullException.ThrowIfNull(activeInstances);
        ArgumentNullException.ThrowIfNull(transport);
        _activeInstances = activeInstances.Distinct().ToArray();
        _transport = transport;
    }

    /// <summary>Connects a real CfSharp session while the current user has no active Adapters.</summary>
    public static MirrorPulseDemandProvider CreateWithoutAdapters() =>
        new([], new NoActiveAdapterRangeTransport());

    public ValueTask<Stream> OpenReadAsync(
        CloudFileFetchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return OpenReadAsync(
            request.NormalizedPath,
            request.FileIdentity.ToArray(),
            request.FileSize,
            request.Offset,
            request.Length,
            cancellationToken);
    }

    public ValueTask<Stream> OpenReadAsync(
        string normalizedPath,
        ReadOnlyMemory<byte> encodedIdentity,
        long fileSize,
        long offset,
        long length,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedPath);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        if (offset > fileSize || length > fileSize - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "The hydration range exceeds the file size.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        CloudPlaceholderIdentity identity = CloudPlaceholderIdentity.Decode(encodedIdentity.Span);
        InstanceId instanceId = ResolveInstance(identity);
        Stream stream = new WorkerRangeStream(
            _transport,
            instanceId,
            normalizedPath,
            encodedIdentity.ToArray(),
            fileSize);
        return ValueTask.FromResult(stream);
    }

    private InstanceId ResolveInstance(CloudPlaceholderIdentity identity)
    {
        foreach (InstanceId candidate in _activeInstances)
        {
            Guid expectedItemId = MirrorPulsePlaceholderIdentity
                .Create(candidate, identity.RemoteId, identity.RemoteRevision)
                .ToCfSharp()
                .ItemId;
            if (expectedItemId == identity.ItemId)
            {
                return candidate;
            }
        }

        throw new FileNotFoundException("The placeholder does not belong to an active Adapter instance.");
    }

    private sealed class NoActiveAdapterRangeTransport : IMirrorPulseWorkerRangeTransport
    {
        public ValueTask<Stream> ReadRangeAsync(
            MirrorPulseWorkerReadRangeRequest request,
            CancellationToken cancellationToken) =>
            ValueTask.FromException<Stream>(new FileNotFoundException("No Adapter instance is active."));
    }

    private sealed class WorkerRangeStream(
        IMirrorPulseWorkerRangeTransport transport,
        InstanceId instanceId,
        string normalizedPath,
        byte[] encodedIdentity,
        long fileSize) : Stream
    {
        private long _position;

        public override bool CanRead => true;

        public override bool CanSeek => true;

        public override bool CanWrite => false;

        public override long Length => fileSize;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > fileSize)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _position = value;
            }
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty || _position == fileSize)
            {
                return 0;
            }

            int length = checked((int)Math.Min(buffer.Length, fileSize - _position));
            var request = new MirrorPulseWorkerReadRangeRequest(
                instanceId,
                normalizedPath,
                encodedIdentity,
                _position,
                length);
            await using Stream source = await transport.ReadRangeAsync(request, cancellationToken)
                .ConfigureAwait(false);
            if (!source.CanRead)
            {
                throw new InvalidDataException("The Adapter Worker returned an unreadable range stream.");
            }

            int received = 0;
            while (received < length)
            {
                int count = await source.ReadAsync(buffer.Slice(received, length - received), cancellationToken)
                    .ConfigureAwait(false);
                if (count == 0)
                {
                    throw new EndOfStreamException("The Adapter Worker returned a truncated range.");
                }

                received += count;
            }

            _position += received;
            return received;
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

        public override long Seek(long offset, SeekOrigin origin)
        {
            long position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => checked(_position + offset),
                SeekOrigin.End => checked(fileSize + offset),
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            Position = position;
            return position;
        }

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

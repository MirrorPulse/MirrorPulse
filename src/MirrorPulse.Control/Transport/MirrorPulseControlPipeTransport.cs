using System.Buffers.Binary;
using System.IO.Pipes;
using MirrorPulse.Control.Contracts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Control.Transport;

/// <summary>
/// Length-prefixed framing used by the versioned control channel.
/// </summary>
public static class MirrorPulseControlPipeTransport
{
    public static async ValueTask<byte[]> ReadFrameAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var frame = await LengthPrefixedFrameReader.ReadAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        if (frame.Length > MirrorPulseControlSchema.MaximumFrameBytes)
        {
            throw new InvalidDataException("The control frame exceeds the protocol limit.");
        }

        return frame;
    }

    public static async ValueTask WriteFrameAsync(
        Stream stream,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
        {
            throw new ArgumentException("The stream must be writable.", nameof(stream));
        }

        if (payload.Length > MirrorPulseControlSchema.MaximumFrameBytes)
        {
            throw new InvalidDataException("The control frame exceeds the protocol limit.");
        }

        var lengthPrefix = new byte[ControlFrameLimits.LengthPrefixBytes];
        ControlFrameLimits.WritePayloadLength((uint)payload.Length, lengthPrefix);
        await stream.WriteAsync(lengthPrefix, cancellationToken).ConfigureAwait(false);
        if (!payload.IsEmpty)
        {
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        }

        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Serves one request at a time on the current-user versioned control pipe.
/// </summary>
public sealed class MirrorPulseControlPipeServer
{
    private readonly Func<ControlRequestEnvelope, CancellationToken, ValueTask<ControlResponseEnvelope>> _handler;
    private readonly string _pipeName;

    public MirrorPulseControlPipeServer(
        Func<ControlRequestEnvelope, CancellationToken, ValueTask<ControlResponseEnvelope>> handler,
        string? pipeName = null)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        _pipeName = string.IsNullOrWhiteSpace(pipeName)
            ? MirrorPulseControlPipeNames.CurrentUserV1()
            : pipeName.Trim();
    }

    public string PipeName => _pipeName;

    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var pipe = SecureNamedPipeServerFactory.Create(
                new NamedPipeServerOptions(_pipeName));
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                var request = MirrorPulseControlJsonCodec.Deserialize<ControlRequestEnvelope>(
                    await MirrorPulseControlPipeTransport.ReadFrameAsync(pipe, cancellationToken)
                        .ConfigureAwait(false));
                var response = await _handler(request, cancellationToken).ConfigureAwait(false);
                await MirrorPulseControlPipeTransport.WriteFrameAsync(
                    pipe,
                    MirrorPulseControlJsonCodec.Serialize(response),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidDataException exception)
            {
                await TryWriteFailureAsync(pipe, Guid.Empty,
                    MirrorPulseControlErrorCodes.InvalidRequest,
                    exception.Message,
                    ErrorCategory.Protocol,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (ArgumentException exception)
            {
                await TryWriteFailureAsync(pipe, Guid.Empty,
                    MirrorPulseControlErrorCodes.InvalidRequest,
                    exception.Message,
                    ErrorCategory.Validation,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                // A disconnected client must not stop the Host control channel.
            }
        }
    }

    private static async Task TryWriteFailureAsync(
        NamedPipeServerStream pipe,
        Guid requestId,
        string code,
        string message,
        ErrorCategory category,
        CancellationToken cancellationToken)
    {
        if (!pipe.IsConnected || requestId == Guid.Empty)
        {
            return;
        }

        var response = ControlResponseEnvelope.Failure(
            requestId,
            new ControlError(code, message, category));
        try
        {
            await MirrorPulseControlPipeTransport.WriteFrameAsync(
                pipe,
                MirrorPulseControlJsonCodec.Serialize(response),
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The peer may have disconnected while the error was being sent.
        }
    }
}

using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Host;

public sealed record MirrorPulseAppInstanceStatus(
    string InstanceId,
    string DisplayName,
    bool Enabled,
    string Phase,
    string? CursorFingerprint,
    DateTimeOffset? CursorUpdatedAt,
    DateTimeOffset? LastSuccessfulSync);

public sealed record MirrorPulseAppStatusResponse(
    int PendingUploads,
    int PendingRemoteConflicts,
    IReadOnlyList<MirrorPulseAppInstanceStatus> Instances,
    string? Error = null);

/// <summary>Current-user, bounded request/response channel from WinUI to the owner Host.</summary>
public sealed class MirrorPulseAppStatusPipe
{
    private const int MaximumFrameBytes = 1024 * 1024;
    private readonly Func<CancellationToken, Task<MirrorPulseAppStatusResponse>> _readStatus;

    public MirrorPulseAppStatusPipe(Func<CancellationToken, Task<MirrorPulseAppStatusResponse>> readStatus)
    {
        _readStatus = readStatus ?? throw new ArgumentNullException(nameof(readStatus));
    }

    public static string CurrentUserPipeName()
    {
        using var identity = WindowsIdentity.GetCurrent();
        string sid = identity.User?.Value ?? throw new InvalidOperationException("The current user has no SID.");
        return $"MirrorPulse-app-{sid}";
    }

    public async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using NamedPipeServerStream pipe = SecureNamedPipeServerFactory.Create(
                new NamedPipeServerOptions(CurrentUserPipeName()));
            try
            {
                await pipe.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                string request = JsonSerializer.Deserialize<string>(
                    await ReadFrameAsync(pipe, cancellationToken).ConfigureAwait(false))
                    ?? string.Empty;
                MirrorPulseAppStatusResponse response;
                if (request != "status")
                {
                    response = new(0, 0, [], "Unknown Host request.");
                }
                else
                {
                    try
                    {
                        response = await _readStatus(cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        response = new(0, 0, [], exception.Message);
                    }
                }

                await WriteFrameAsync(pipe, JsonSerializer.SerializeToUtf8Bytes(response), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // One disconnected UI client cannot stop the Host status channel.
            }
            catch (JsonException)
            {
                // A malformed request from one client cannot stop the Host status channel.
            }
            catch (InvalidDataException)
            {
                // An oversized request from one client cannot stop the Host status channel.
            }
        }
    }

    public static async Task<MirrorPulseAppStatusResponse> RequestAsync(
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        await using NamedPipeClientStream pipe = await NamedPipeWorkerClient.ConnectAsync(
            CurrentUserPipeName(), TimeSpan.FromSeconds(5), timeout.Token).ConfigureAwait(false);
        await WriteFrameAsync(pipe, JsonSerializer.SerializeToUtf8Bytes("status"), timeout.Token)
            .ConfigureAwait(false);
        byte[] payload = await ReadFrameAsync(pipe, timeout.Token).ConfigureAwait(false);
        MirrorPulseAppStatusResponse response = JsonSerializer.Deserialize<MirrorPulseAppStatusResponse>(payload)
            ?? throw new InvalidDataException("The Host returned an empty status response.");
        if (response.Error is not null)
        {
            throw new InvalidOperationException(response.Error);
        }

        return response;
    }

    private static async Task<byte[]> ReadFrameAsync(Stream stream, CancellationToken cancellationToken)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken).ConfigureAwait(false);
        int length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaximumFrameBytes)
        {
            throw new InvalidDataException("The Host status frame is too large or empty.");
        }

        byte[] payload = new byte[length];
        await stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
        return payload;
    }

    private static async Task WriteFrameAsync(Stream stream, byte[] payload, CancellationToken cancellationToken)
    {
        if (payload.Length is < 1 or > MaximumFrameBytes)
        {
            throw new InvalidDataException("The Host status frame is too large or empty.");
        }

        byte[] header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);
        await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}

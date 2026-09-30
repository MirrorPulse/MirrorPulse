using MirrorPulse.Control.Transport;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Control.Client;

/// <summary>
/// Locates the current user's Host through its versioned control channel.
/// </summary>
public sealed class MirrorPulseHostLocator
{
    public MirrorPulseHostLocator(string? pipeName = null)
    {
        PipeName = string.IsNullOrWhiteSpace(pipeName)
            ? MirrorPulseControlPipeNames.CurrentUserV1()
            : pipeName.Trim();
    }

    public string PipeName { get; }

    public async Task<bool> IsRunningAsync(
        TimeSpan timeout = default,
        CancellationToken cancellationToken = default)
    {
        if (timeout == default)
        {
            timeout = TimeSpan.FromMilliseconds(250);
        }

        try
        {
            await using var pipe = await NamedPipeWorkerClient.ConnectAsync(
                PipeName, timeout, cancellationToken).ConfigureAwait(false);
            return pipe.IsConnected;
        }
        catch (IOException)
        {
            return false;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}

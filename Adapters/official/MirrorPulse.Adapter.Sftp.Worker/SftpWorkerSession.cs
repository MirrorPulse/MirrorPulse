using System.Text.Json;
using MirrorPulse.Adapter.Sdk;

namespace MirrorPulse.Adapter.Sftp.Worker;

public sealed record SftpWorkerConfiguration(Uri Endpoint, string Username, string CredentialReference)
{
    public void Validate()
    {
        if (!Endpoint.IsAbsoluteUri || !string.Equals(Endpoint.Scheme, "sftp", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Endpoint.Host) || !string.IsNullOrWhiteSpace(Endpoint.UserInfo) ||
            !string.IsNullOrEmpty(Endpoint.Query) || !string.IsNullOrEmpty(Endpoint.Fragment) ||
            string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(CredentialReference))
        {
            throw new InvalidDataException("The SFTP endpoint, username, or credential reference is invalid.");
        }
    }
}

public static class SftpWorkerProgram
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken = default)
    {
        AdapterWorkerProcessArguments arguments;
        try
        {
            arguments = AdapterWorkerProcessArguments.Parse(args);
        }
        catch (ArgumentException)
        {
            return 2;
        }

        await using AdapterNamedPipeClient pipe = await AdapterNamedPipeClient.ConnectAsync(
            arguments.PipeName, TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
        var channel = new AdapterControlChannel(pipe, arguments.InstanceId, arguments.WorkerSessionId);
        Guid helloId = Guid.NewGuid();
        await channel.SendAsync("Hello", helloId, false,
            new { adapterId = "mirrorpulse.sftp", minimumProtocolVersion = 1, maximumProtocolVersion = 1 },
            cancellationToken).ConfigureAwait(false);

        try
        {
            AdapterControlFrame ready = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (ready.MessageType != "Ready" || !ready.IsResponse || ready.RequestId != helloId)
            {
                throw new InvalidDataException("The Host did not accept the SFTP Worker handshake.");
            }

            SftpWorkerConfiguration configuration = ready.Payload.Deserialize<SftpWorkerConfiguration>(JsonOptions)
                ?? throw new InvalidDataException("The Host did not provide SFTP configuration.");
            configuration.Validate();
            await channel.SendAsync("Configured", helloId, false,
                new { endpointHost = configuration.Endpoint.Host }, cancellationToken).ConfigureAwait(false);

            while (true)
            {
                AdapterControlFrame command = await channel.ReadAsync(cancellationToken).ConfigureAwait(false);
                if (command.IsResponse || command.MessageType != "Stop")
                {
                    throw new InvalidDataException("The SFTP Worker received an unsupported command.");
                }

                await channel.SendAsync("Stopped", command.RequestId, true, new { }, cancellationToken)
                    .ConfigureAwait(false);
                return 0;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 0;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or KeyNotFoundException)
        {
            await channel.SendAsync("Error", helloId, false,
                new { code = "InvalidConfiguration" }, CancellationToken.None).ConfigureAwait(false);
            return 1;
        }
    }
}

public sealed class SftpWorkerEntryMarker;

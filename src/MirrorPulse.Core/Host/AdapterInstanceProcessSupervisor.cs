using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Security;
using MirrorPulse.Core.State;
using MirrorPulse.Core.Transport;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Host;

/// <summary>Owns one isolated process and current-user control pipe per enabled Adapter instance.</summary>
public sealed class AdapterInstanceProcessSupervisor : IAsyncDisposable
{
    private readonly MirrorPulseProductCatalog _catalog;
    private readonly ISecureCredentialStore _credentials;
    private readonly CancellationTokenSource _shutdown = new();
    private Task[] _workers = [];
    private bool _started;

    public AdapterInstanceProcessSupervisor(MirrorPulseProductCatalog catalog, ISecureCredentialStore credentials)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
    }

    public async Task StartAsync(MirrorPulseAdapterTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        if (_started)
        {
            throw new InvalidOperationException("The Adapter process supervisor is already running.");
        }

        _started = true;
        foreach (AdapterInstance instance in topology.Instances.Where(item => !item.Enabled))
        {
            await SetPhaseAsync(instance.InstanceId, "Offline", _shutdown.Token).ConfigureAwait(false);
        }

        _workers = topology.Instances.Where(item => item.Enabled)
            .Select(instance => RunInstanceAsync(topology, instance, _shutdown.Token)).ToArray();
    }

    private async Task RunInstanceAsync(
        MirrorPulseAdapterTopology topology,
        AdapterInstance instance,
        CancellationToken cancellationToken)
    {
        try
        {
            await SetPhaseAsync(instance.InstanceId, "Starting", cancellationToken).ConfigureAwait(false);
            WorkerSessionId sessionId = WorkerSessionId.New();
            string runtimeIdentifier = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.X64 => "win-x64",
                System.Runtime.InteropServices.Architecture.Arm64 => "win-arm64",
                _ => throw new PlatformNotSupportedException("The Adapter process architecture is unsupported."),
            };
            string pipeName = $"mirrorpulse-adapter-{Guid.NewGuid():N}";
            string[] arguments = ["--instance-id", instance.InstanceId.ToString(),
                "--worker-session-id", sessionId.ToString(), "--pipe-name", pipeName];
            AdapterInstanceWorkerPayload payload = AdapterInstanceWorkerLaunchResolver.Resolve(
                topology, instance.InstanceId, sessionId, runtimeIdentifier, arguments);
            WorkerLaunchRequest request = payload.LaunchRequest;
            request = new WorkerLaunchRequest(request.InstanceId, request.WorkerSessionId,
                request.ExecutablePath, request.WorkingDirectory, request.Arguments,
                new Dictionary<string, string> { ["MP_TRANSFER_CACHE_DIR"] = instance.TransferCacheDirectory });
            Directory.CreateDirectory(instance.TransferCacheDirectory);
            await using var pipe = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions(pipeName));
            using var job = WorkerJobObject.Create();
            using WorkerProcessHandle worker = WorkerProcessLauncher.Start(request);
            try
            {
                job.Attach(worker.Process);
                using var connectionTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectionTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                await pipe.WaitForConnectionAsync(connectionTimeout.Token).ConfigureAwait(false);
                await ServeWorkerAsync(pipe, instance, sessionId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (pipe.IsConnected && !worker.Process.HasExited)
                {
                    try
                    {
                        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                        await WriteAsync(pipe, new ControlFrameEnvelope(1, "Stop", Guid.NewGuid(),
                            instance.InstanceId, sessionId, false, JsonSerializer.SerializeToElement(new { })),
                            stopTimeout.Token).ConfigureAwait(false);
                        await worker.WaitForExitAsync(stopTimeout.Token).ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is IOException or OperationCanceledException or
                        EndOfStreamException)
                    {
                        // Disposing the Job Object below terminates the remaining process tree.
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            await SetPhaseAsync(instance.InstanceId, "Worker failed", CancellationToken.None,
                exception.GetType().Name).ConfigureAwait(false);
        }
    }

    private async Task ServeWorkerAsync(
        NamedPipeServerStream pipe,
        AdapterInstance instance,
        WorkerSessionId sessionId,
        CancellationToken cancellationToken)
    {
        ControlFrameEnvelope hello = await ReadAsync(pipe, cancellationToken).ConfigureAwait(false);
        ValidateFrame(hello, "Hello", instance.InstanceId, sessionId);
        await WriteAsync(pipe, new ControlFrameEnvelope(1, "Ready", hello.RequestId,
            instance.InstanceId, sessionId, true, JsonSerializer.SerializeToElement(instance.Configuration)),
            cancellationToken).ConfigureAwait(false);
        while (true)
        {
            ControlFrameEnvelope frame = await ReadAsync(pipe, cancellationToken).ConfigureAwait(false);
            ValidateFrame(frame, null, instance.InstanceId, sessionId);
            switch (frame.MessageType)
            {
                case "CredentialRequest":
                    await AnswerCredentialAsync(pipe, instance, sessionId, frame, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case "HostKeyChallenge":
                    await WriteAsync(pipe, new ControlFrameEnvelope(1, "HostKeyDecision", frame.RequestId,
                        instance.InstanceId, sessionId, true,
                        JsonSerializer.SerializeToElement(new
                        {
                            sha256 = frame.Payload.GetProperty("sha256").GetString(),
                            approved = false,
                        })), cancellationToken).ConfigureAwait(false);
                    break;
                case "Connected":
                    await SetPhaseAsync(instance.InstanceId, "Connected", cancellationToken).ConfigureAwait(false);
                    break;
                case "Error":
                    string code = frame.Payload.TryGetProperty("code", out JsonElement value)
                        ? value.GetString() ?? "Unknown" : "Unknown";
                    await SetPhaseAsync(instance.InstanceId, "Worker error", cancellationToken, code)
                        .ConfigureAwait(false);
                    return;
                default:
                    throw new InvalidDataException("The Adapter sent an unexpected control message.");
            }
        }
    }

    private async Task AnswerCredentialAsync(
        Stream pipe,
        AdapterInstance instance,
        WorkerSessionId sessionId,
        ControlFrameEnvelope request,
        CancellationToken cancellationToken)
    {
        string referenceId = request.Payload.GetProperty("referenceId").GetString()
            ?? throw new InvalidDataException("The Worker credential reference is missing.");
        if (!instance.CredentialReferences.Contains(referenceId, StringComparer.Ordinal))
        {
            throw new UnauthorizedAccessException("The Worker requested a credential outside its instance.");
        }

        var reference = new CredentialReference(referenceId, CredentialKind.Password,
            instance.AdapterId.ToString(), CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        using SecureCredentialValue stored = await _credentials.TryGetAsync(reference, cancellationToken)
            .ConfigureAwait(false) ?? throw new KeyNotFoundException("The Adapter credential was not found.");
        string secret = Encoding.UTF8.GetString(stored.Value.Span);
        await WriteAsync(pipe, new ControlFrameEnvelope(1, "CredentialResponse", request.RequestId,
            instance.InstanceId, sessionId, true,
            JsonSerializer.SerializeToElement(new { referenceId, secret })), cancellationToken)
            .ConfigureAwait(false);
    }

    private Task SetPhaseAsync(
        InstanceId instanceId,
        string phase,
        CancellationToken cancellationToken,
        string? errorCode = null) =>
        _catalog.SaveInstanceRuntimeStateAsync(new(instanceId, phase, false, null, errorCode), cancellationToken);

    private static void ValidateFrame(
        ControlFrameEnvelope frame,
        string? expectedMessage,
        InstanceId instanceId,
        WorkerSessionId sessionId)
    {
        if (frame.ProtocolVersion != 1 || frame.InstanceId != instanceId || frame.WorkerSessionId != sessionId ||
            frame.IsResponse || (expectedMessage is not null && frame.MessageType != expectedMessage))
        {
            throw new InvalidDataException("The Adapter control frame does not belong to this session.");
        }
    }

    private static async Task<ControlFrameEnvelope> ReadAsync(Stream pipe, CancellationToken cancellationToken) =>
        ControlFrameJsonCodec.Decode(await LengthPrefixedFrameReader.ReadAsync(pipe, cancellationToken)
            .ConfigureAwait(false));

    private static async Task WriteAsync(Stream pipe, ControlFrameEnvelope frame, CancellationToken cancellationToken)
    {
        byte[] payload = ControlFrameJsonCodec.Encode(frame);
        byte[] prefix = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix, checked((uint)payload.Length));
        await pipe.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await pipe.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await pipe.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _shutdown.Cancel();
        await Task.WhenAll(_workers).ConfigureAwait(false);
        _shutdown.Dispose();
    }
}

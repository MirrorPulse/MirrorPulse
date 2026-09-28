using System.Buffers.Binary;
using System.Text.Json;
using MirrorPulse.Adapter.Sftp.Worker;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SftpWorkerProcessTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IndependentWorkerHandshakesAndReturnsStructuredConfigurationError(bool invalid)
    {
        var instance = InstanceId.New();
        var session = WorkerSessionId.New();
        string pipeName = $"mirrorpulse-sftp-{Guid.NewGuid():N}";
        await using var pipe = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions(pipeName));
        string executable = Path.ChangeExtension(typeof(SftpWorkerEntryMarker).Assembly.Location, ".exe");
        using WorkerProcessHandle worker = WorkerProcessLauncher.Start(new WorkerLaunchRequest(
            instance, session, executable, Path.GetDirectoryName(executable)!,
            ["--instance-id", instance.ToString(), "--worker-session-id", session.ToString(),
             "--pipe-name", pipeName]));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token);
            ControlFrameEnvelope hello = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Hello", hello.MessageType);
            Assert.AreEqual("mirrorpulse.sftp", hello.Payload.GetProperty("adapterId").GetString());
            await WriteAsync(pipe, new ControlFrameEnvelope(1, "Ready", hello.RequestId,
                instance, session, true, JsonSerializer.SerializeToElement(new
                {
                    endpoint = invalid ? "https://user:secret@example.test/" : "sftp://example.test/",
                    username = "user",
                    credentialReference = "password-reference",
                })), timeout.Token);
            ControlFrameEnvelope response = await ReadAsync(pipe, timeout.Token);
            if (invalid)
            {
                Assert.AreEqual("Error", response.MessageType);
                Assert.AreEqual("InvalidConfiguration", response.Payload.GetProperty("code").GetString());
                Assert.IsFalse(response.Payload.ToString().Contains("secret", StringComparison.Ordinal));
                await worker.WaitForExitAsync(timeout.Token);
                Assert.AreEqual(1, worker.Process.ExitCode);
                return;
            }

            Assert.AreEqual("Configured", response.MessageType);
            Guid stopId = Guid.NewGuid();
            await WriteAsync(pipe, new ControlFrameEnvelope(1, "Stop", stopId,
                instance, session, false, JsonSerializer.SerializeToElement(new { })), timeout.Token);
            response = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Stopped", response.MessageType);
            Assert.AreEqual(stopId, response.RequestId);
            await worker.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, worker.Process.ExitCode);
        }
        finally
        {
            if (!worker.Process.HasExited)
            {
                worker.Process.Kill(entireProcessTree: true);
                await worker.WaitForExitAsync();
            }
        }
    }

    private static async Task<ControlFrameEnvelope> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        ControlFrameJsonCodec.Decode(await LengthPrefixedFrameReader.ReadAsync(stream, cancellationToken));

    private static async Task WriteAsync(Stream stream, ControlFrameEnvelope envelope, CancellationToken cancellationToken)
    {
        byte[] payload = ControlFrameJsonCodec.Encode(envelope);
        byte[] frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
        payload.CopyTo(frame.AsSpan(4));
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}

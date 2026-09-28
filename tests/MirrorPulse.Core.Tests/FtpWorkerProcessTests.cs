using System.Buffers.Binary;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using MirrorPulse.Adapter.Ftp.Worker;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class FtpWorkerProcessTests
{
    [TestMethod]
    public async Task IndependentWorkerAuthenticatesPlainExplicitAndImplicitFtpOverCurrentUserPipe()
    {
        foreach (FtpSecurityMode mode in Enum.GetValues<FtpSecurityMode>())
        {
            await VerifyModeAsync(mode);
        }
    }

    [TestMethod]
    public async Task IndependentWorkerRejectsInvalidHostConfigurationBeforeRequestingCredential()
    {
        var instance = InstanceId.New();
        var session = WorkerSessionId.New();
        string pipeName = $"mirrorpulse-ftp-{Guid.NewGuid():N}";
        await using var pipe = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions(pipeName));
        string executable = Path.ChangeExtension(typeof(FtpWorkerEntryMarker).Assembly.Location, ".exe");
        var request = new WorkerLaunchRequest(instance, session, executable,
            Path.GetDirectoryName(executable)!,
            ["--instance-id", instance.ToString(), "--worker-session-id", session.ToString(),
             "--pipe-name", pipeName]);
        using WorkerProcessHandle worker = WorkerProcessLauncher.Start(request);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token);
            ControlFrameEnvelope hello = await ReadAsync(pipe, timeout.Token);
            await WriteAsync(pipe, new ControlFrameEnvelope(1, "Ready", hello.RequestId,
                instance, session, true, JsonSerializer.SerializeToElement(new
                {
                    endpoint = "https://user:secret@example.test/",
                    username = "user",
                    credentialReference = "ftp-password",
                    securityMode = "ExplicitTls",
                })), timeout.Token);

            ControlFrameEnvelope error = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Error", error.MessageType);
            Assert.AreEqual("InvalidConfiguration", error.Payload.GetProperty("code").GetString());
            Assert.IsFalse(error.Payload.ToString().Contains("secret", StringComparison.Ordinal));
            await worker.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(1, worker.Process.ExitCode);
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

    private static async Task VerifyModeAsync(FtpSecurityMode mode)
    {
        await using var fixture = new LoopbackFtpFixture(mode);
        var instance = InstanceId.New();
        var session = WorkerSessionId.New();
        string pipeName = $"mirrorpulse-ftp-{Guid.NewGuid():N}";
        await using var pipe = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions(pipeName));
        string executable = Path.ChangeExtension(typeof(FtpWorkerEntryMarker).Assembly.Location, ".exe");
        Assert.IsTrue(File.Exists(executable), "The independently built FTP Worker executable is missing.");
        var request = new WorkerLaunchRequest(instance, session, executable,
            Path.GetDirectoryName(executable)!,
            ["--instance-id", instance.ToString(), "--worker-session-id", session.ToString(),
             "--pipe-name", pipeName]);
        using WorkerProcessHandle worker = WorkerProcessLauncher.Start(request);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await pipe.WaitForConnectionAsync(timeout.Token);
            ControlFrameEnvelope hello = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Hello", hello.MessageType);
            Assert.AreEqual(instance, hello.InstanceId);
            Assert.AreEqual(session, hello.WorkerSessionId);
            Assert.AreEqual("mirrorpulse.ftp", hello.Payload.GetProperty("adapterId").GetString());

            var ready = new ControlFrameEnvelope(1, "Ready", hello.RequestId, instance, session, true,
                JsonSerializer.SerializeToElement(new
                {
                    endpoint = $"ftp://127.0.0.1:{fixture.Port}/",
                    username = "user",
                    credentialReference = "ftp-password",
                    securityMode = mode.ToString(),
                    trustedCertificateSha256 = mode == FtpSecurityMode.Plain
                        ? null : fixture.CertificateSha256,
                }));
            await WriteAsync(pipe, ready, timeout.Token);

            ControlFrameEnvelope credentialRequest = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("CredentialRequest", credentialRequest.MessageType);
            Assert.AreEqual("ftp-password", credentialRequest.Payload.GetProperty("referenceId").GetString());
            await WriteAsync(pipe, new ControlFrameEnvelope(1, "CredentialResponse",
                credentialRequest.RequestId, instance, session, true,
                JsonSerializer.SerializeToElement(new { referenceId = "ftp-password", secret = "correct-secret" })),
                timeout.Token);

            ControlFrameEnvelope connected = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Connected", connected.MessageType,
                connected.MessageType == "Error" ? connected.Payload.ToString() : string.Empty);
            Assert.AreEqual(mode != FtpSecurityMode.Plain,
                connected.Payload.GetProperty("encrypted").GetBoolean());

            Guid stopId = Guid.NewGuid();
            await WriteAsync(pipe, new ControlFrameEnvelope(1, "Stop", stopId, instance, session, false,
                JsonSerializer.SerializeToElement(new { })), timeout.Token);
            ControlFrameEnvelope stopped = await ReadAsync(pipe, timeout.Token);
            Assert.AreEqual("Stopped", stopped.MessageType);
            Assert.AreEqual(stopId, stopped.RequestId);
            await worker.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, worker.Process.ExitCode);
            Assert.IsTrue(fixture.Authenticated);
            Assert.AreEqual(mode != FtpSecurityMode.Plain, fixture.ControlChannelEncrypted);
            Assert.IsFalse(request.Arguments.Any(argument => argument.Contains("correct-secret", StringComparison.Ordinal)));
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

    private static async ValueTask<ControlFrameEnvelope> ReadAsync(Stream stream, CancellationToken cancellationToken) =>
        ControlFrameJsonCodec.Decode(await LengthPrefixedFrameReader.ReadAsync(stream, cancellationToken));

    private static async ValueTask WriteAsync(
        Stream stream,
        ControlFrameEnvelope envelope,
        CancellationToken cancellationToken)
    {
        byte[] payload = ControlFrameJsonCodec.Encode(envelope);
        var frame = new byte[4 + payload.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
        payload.CopyTo(frame.AsSpan(4));
        await stream.WriteAsync(frame, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private sealed class LoopbackFtpFixture : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly X509Certificate2 _certificate;
        private readonly Task _server;
        private readonly FtpSecurityMode _mode;

        public LoopbackFtpFixture(FtpSecurityMode mode)
        {
            _mode = mode;
            using RSA key = RSA.Create(2048);
            var certificateRequest = new CertificateRequest(
                "CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using X509Certificate2 generated = certificateRequest.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
            _certificate = X509CertificateLoader.LoadPkcs12(
                generated.Export(X509ContentType.Pfx), null,
                X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
            CertificateSha256 = Convert.ToHexString(SHA256.HashData(_certificate.RawData));
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _server = ServeAsync();
        }

        public int Port { get; }

        public string CertificateSha256 { get; }

        public bool Authenticated { get; private set; }

        public bool ControlChannelEncrypted { get; private set; }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            try
            {
                await _server.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException or TimeoutException)
            {
            }

            _certificate.Dispose();
        }

        private async Task ServeAsync()
        {
            using TcpClient client = await _listener.AcceptTcpClientAsync();
            Stream stream = client.GetStream();
            if (_mode == FtpSecurityMode.ImplicitTls)
            {
                stream = await SecureAsync(stream);
            }

            await SendAsync(stream, "220 MirrorPulse test FTP ready\r\n");
            while (true)
            {
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                string? line = await reader.ReadLineAsync();
                if (line is null)
                {
                    return;
                }

                string command = line.Split(' ', 2)[0].ToUpperInvariant();
                string argument = line.Length > command.Length ? line[(command.Length + 1)..] : string.Empty;
                switch (command)
                {
                    case "AUTH" when _mode == FtpSecurityMode.ExplicitTls && argument == "TLS":
                        await SendAsync(stream, "234 Proceed with TLS\r\n");
                        stream = await SecureAsync(stream);
                        break;
                    case "USER":
                        await SendAsync(stream, argument == "user" ? "331 Password required\r\n" : "530 Invalid user\r\n");
                        break;
                    case "PASS":
                        Authenticated = argument == "correct-secret";
                        await SendAsync(stream, Authenticated ? "230 Logged in\r\n" : "530 Login incorrect\r\n");
                        break;
                    case "FEAT":
                        await SendAsync(stream, "211-Features\r\n UTF8\r\n211 End\r\n");
                        break;
                    case "SYST":
                        await SendAsync(stream, "215 UNIX Type: L8\r\n");
                        break;
                    case "PWD":
                        await SendAsync(stream, "257 \"/\" is current directory\r\n");
                        break;
                    case "QUIT":
                        await SendAsync(stream, "221 Goodbye\r\n");
                        return;
                    default:
                        await SendAsync(stream, "200 Command okay\r\n");
                        break;
                }
            }
        }

        private async Task<Stream> SecureAsync(Stream input)
        {
            var tls = new SslStream(input, leaveInnerStreamOpen: true);
            await tls.AuthenticateAsServerAsync(_certificate, clientCertificateRequired: false,
                enabledSslProtocols: SslProtocols.Tls12 | SslProtocols.Tls13,
                checkCertificateRevocation: false);
            ControlChannelEncrypted = true;
            return tls;
        }

        private static Task SendAsync(Stream stream, string response) =>
            stream.WriteAsync(Encoding.ASCII.GetBytes(response)).AsTask();
    }
}

using System.Buffers.Binary;
using System.Text.Json;
using MirrorPulse.Adapter.Sftp.Worker;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Transport;
using MirrorPulse.Core.Workers;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class SftpWorkerTransferTests
{
    [TestMethod]
    public async Task RangeConditionalUploadConflictAndReconnectRetryUseRealSftpWorker()
    {
        await using SftpProtocolFixture fixture = await SftpProtocolFixture.StartAsync();
        string file = Path.Combine(fixture.StorageDirectory, "report.bin");
        await File.WriteAllBytesAsync(file, [2, 5, 7, 11, 13]);
        Guid retryId = Guid.NewGuid();
        await using (var worker = await WorkerHarness.StartAsync(fixture))
        {
            Guid readId = Guid.NewGuid();
            await worker.SendAsync("ReadRange", readId, new { path = "report.bin", offset = 2, length = 3 });
            ControlFrameEnvelope ready = await worker.ReadAsync();
            Assert.AreEqual("ReadRangeReady", ready.MessageType, ready.Payload.ToString());
            BinaryChunkFrame chunk = BinaryChunkCodec.Decode(
                await LengthPrefixedFrameReader.ReadAsync(worker.Pipe, worker.Token));
            CollectionAssert.AreEqual(new byte[] { 7, 11, 13 }, chunk.Data.ToArray());
            Assert.AreEqual(2, chunk.Offset);
            Assert.IsTrue(chunk.EndOfStream);

            await worker.SendAsync("Stat", Guid.NewGuid(), new { path = "report.bin" });
            ControlFrameEnvelope stat = await worker.ReadAsync();
            Assert.AreEqual("StatResult", stat.MessageType, stat.Payload.ToString());
            string oldRevision = stat.Payload.GetProperty("revision").GetString()!;

            ControlFrameEnvelope complete = await worker.UploadAsync(Guid.NewGuid(),
                "report.bin", oldRevision, [1, 4, 9, 16]);
            Assert.AreEqual("UploadComplete", complete.MessageType, complete.Payload.ToString());
            worker.AssertTransferCacheEmpty();
            CollectionAssert.AreEqual(new byte[] { 1, 4, 9, 16 }, await File.ReadAllBytesAsync(file));

            ControlFrameEnvelope conflict = await worker.UploadAsync(Guid.NewGuid(),
                "report.bin", oldRevision, [8, 8, 8]);
            Assert.AreEqual("OperationError", conflict.MessageType);
            Assert.AreEqual("RemoteConflict", conflict.Payload.GetProperty("code").GetString());
            CollectionAssert.AreEqual(new byte[] { 1, 4, 9, 16 }, await File.ReadAllBytesAsync(file));

            await worker.SendAsync("ReadRange", Guid.NewGuid(),
                new { path = "../outside", offset = 0, length = 1 });
            ControlFrameEnvelope unsafePath = await worker.ReadAsync();
            Assert.AreEqual("InvalidRequest", unsafePath.Payload.GetProperty("code").GetString());

            await File.WriteAllTextAsync(Path.Combine(fixture.StorageDirectory, "fail-first-write"), "once");
            ControlFrameEnvelope disconnected = await worker.UploadAsync(retryId,
                "new.bin", null, [6, 2, 6]);
            Assert.AreEqual("OperationError", disconnected.MessageType);
            Assert.AreEqual("RetryableTransferFailure", disconnected.Payload.GetProperty("code").GetString());
            Assert.IsFalse(File.Exists(Path.Combine(fixture.StorageDirectory, "new.bin")));
        }

        await using (var resumed = await WorkerHarness.StartAsync(fixture))
        {
            ControlFrameEnvelope completed = await resumed.UploadAsync(retryId,
                "new.bin", null, [6, 2, 6]);
            Assert.AreEqual("UploadComplete", completed.MessageType, completed.Payload.ToString());
            resumed.AssertTransferCacheEmpty();
            CollectionAssert.AreEqual(new byte[] { 6, 2, 6 },
                await File.ReadAllBytesAsync(Path.Combine(fixture.StorageDirectory, "new.bin")));
        }
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private readonly WorkerProcessHandle _worker;
        private readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(35));
        private readonly string _cache;

        private WorkerHarness(WorkerProcessHandle worker, Stream pipe, InstanceId instance,
            WorkerSessionId session, string cache)
        {
            _worker = worker;
            Pipe = pipe;
            Instance = instance;
            Session = session;
            _cache = cache;
        }

        public Stream Pipe { get; }

        public InstanceId Instance { get; }

        public WorkerSessionId Session { get; }

        public CancellationToken Token => _timeout.Token;

        public void AssertTransferCacheEmpty() => Assert.IsEmpty(Directory.EnumerateFiles(_cache));

        public static async Task<WorkerHarness> StartAsync(SftpProtocolFixture fixture)
        {
            var instance = InstanceId.New();
            var session = WorkerSessionId.New();
            string pipeName = $"mirrorpulse-sftp-transfer-{Guid.NewGuid():N}";
            string cache = Path.Combine(Path.GetTempPath(), $"mirrorpulse-sftp-cache-{Guid.NewGuid():N}");
            var pipe = SecureNamedPipeServerFactory.Create(new NamedPipeServerOptions(pipeName));
            string executable = Path.ChangeExtension(typeof(SftpWorkerEntryMarker).Assembly.Location, ".exe");
            WorkerProcessHandle process = WorkerProcessLauncher.Start(new WorkerLaunchRequest(
                instance, session, executable, Path.GetDirectoryName(executable)!,
                ["--instance-id", instance.ToString(), "--worker-session-id", session.ToString(),
                 "--pipe-name", pipeName],
                new Dictionary<string, string> { ["MP_TRANSFER_CACHE_DIR"] = cache }));
            var harness = new WorkerHarness(process, pipe, instance, session, cache);
            try
            {
                await pipe.WaitForConnectionAsync(harness.Token);
                ControlFrameEnvelope hello = await harness.ReadAsync();
                await harness.SendAsync("Ready", hello.RequestId, new
                {
                    endpoint = $"sftp://127.0.0.1:{fixture.Port}/",
                    username = "user",
                    credentialReference = "sftp-password",
                    trustedHostKeySha256 = fixture.Fingerprint,
                }, isResponse: true);
                ControlFrameEnvelope credential = await harness.ReadAsync();
                Assert.AreEqual("CredentialRequest", credential.MessageType);
                await harness.SendAsync("CredentialResponse", credential.RequestId,
                    new { referenceId = "sftp-password", secret = "correct-secret" }, isResponse: true);
                ControlFrameEnvelope connected = await harness.ReadAsync();
                Assert.AreEqual("Connected", connected.MessageType, connected.Payload.ToString());
                return harness;
            }
            catch
            {
                await harness.DisposeAsync();
                throw;
            }
        }

        public async Task<ControlFrameEnvelope> UploadAsync(
            Guid requestId, string path, string? expectedRevision, byte[] content)
        {
            Guid streamId = Guid.NewGuid();
            await SendAsync("Upload", requestId,
                new { path, expectedRevision, length = content.Length, streamId });
            ControlFrameEnvelope ready = await ReadAsync();
            Assert.AreEqual("UploadReady", ready.MessageType, ready.Payload.ToString());
            byte[] payload = BinaryChunkCodec.Encode(new BinaryChunkFrame(
                requestId, Instance, Session, streamId, 0, content, true, Sha256Digest.Compute(content)));
            await WritePayloadAsync(payload);
            while (true)
            {
                ControlFrameEnvelope response = await ReadAsync();
                if (response.MessageType != "TransferProgress")
                {
                    return response;
                }
            }
        }

        public async Task<ControlFrameEnvelope> ReadAsync() =>
            ControlFrameJsonCodec.Decode(await LengthPrefixedFrameReader.ReadAsync(Pipe, Token));

        public Task SendAsync(string messageType, Guid requestId, object payload, bool isResponse = false) =>
            WritePayloadAsync(ControlFrameJsonCodec.Encode(new ControlFrameEnvelope(
                1, messageType, requestId, Instance, Session, isResponse,
                JsonSerializer.SerializeToElement(payload))));

        private async Task WritePayloadAsync(byte[] payload)
        {
            byte[] frame = new byte[4 + payload.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)payload.Length));
            payload.CopyTo(frame.AsSpan(4));
            await Pipe.WriteAsync(frame, Token);
            await Pipe.FlushAsync(Token);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_worker.Process.HasExited)
            {
                _worker.Process.Kill(entireProcessTree: true);
                await _worker.WaitForExitAsync();
            }

            _worker.Dispose();
            await Pipe.DisposeAsync();
            _timeout.Dispose();
            if (Directory.Exists(_cache))
            {
                Assert.IsEmpty(Directory.EnumerateFiles(_cache));
                Directory.Delete(_cache, recursive: true);
            }
        }
    }
}

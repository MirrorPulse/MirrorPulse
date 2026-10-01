using System.IO.Pipes;
using System.Text;
using MirrorPulse.Adapter.Sdk;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterWorkerUploadClientTests
{
    [TestMethod]
    public async Task UploadStreamsChunksAndReturnsWorkerRevision()
    {
        Guid operationId = Guid.NewGuid();
        InstanceId instanceId = InstanceId.New();
        await UploadInNewSessionAsync(operationId, instanceId);
        await UploadInNewSessionAsync(operationId, instanceId);
    }

    private static async Task UploadInNewSessionAsync(Guid operationId, InstanceId instanceId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string pipeName = $"mirrorpulse-upload-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connection = server.WaitForConnectionAsync(timeout.Token);
        await using AdapterNamedPipeClient workerPipe = await AdapterNamedPipeClient.ConnectAsync(
            pipeName, TimeSpan.FromSeconds(10), timeout.Token);
        await connection;
        WorkerSessionId sessionId = WorkerSessionId.New();
        var worker = new AdapterControlChannel(workerPipe, instanceId.Value, sessionId.Value);
        var channel = new AdapterWorkerReadRangeClient(server, instanceId, sessionId);
        var upload = new AdapterWorkerUploadClient(channel, instanceId, sessionId);
        byte[] content = Encoding.UTF8.GetBytes("upload-content");
        await using var source = new MemoryStream(content, writable: false);
        Task<string> pending = upload.UploadAsync(new MirrorPulseWorkerUploadRequest(
            instanceId, "notes.txt", "old-revision", source, content.Length, operationId), timeout.Token).AsTask();

        AdapterControlFrame command = await worker.ReadAsync(timeout.Token);
        Assert.AreEqual("Upload", command.MessageType);
        Assert.AreEqual(operationId, command.RequestId);
        Assert.AreEqual(operationId, command.Payload.GetProperty("operationId").GetGuid());
        Assert.AreEqual("old-revision", command.Payload.GetProperty("expectedRevision").GetString());
        Guid streamId = command.Payload.GetProperty("streamId").GetGuid();
        Assert.AreEqual(operationId, streamId);
        Task<byte[]> responseBytes = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("UploadReady", command.RequestId, true, new { streamId }, timeout.Token);
        ControlFrameEnvelope response = ControlFrameJsonCodec.Decode(await responseBytes);
        await upload.HandleResponseAsync(response);
        AdapterBinaryChunk received = await worker.ReadChunkAsync(timeout.Token);
        CollectionAssert.AreEqual(content, received.Data.ToArray());
        Assert.IsTrue(received.EndOfStream);

        Task<byte[]> completeBytes = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("UploadComplete", command.RequestId, true,
            new { revision = "new-revision" }, timeout.Token);
        ControlFrameEnvelope complete = ControlFrameJsonCodec.Decode(await completeBytes);
        await upload.HandleResponseAsync(complete);
        Assert.AreEqual("new-revision", await pending);
        upload.Close();
        channel.Close();
    }

    [TestMethod]
    public async Task StatReturnsNullableRevision()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string pipeName = $"mirrorpulse-stat-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connection = server.WaitForConnectionAsync(timeout.Token);
        await using AdapterNamedPipeClient workerPipe = await AdapterNamedPipeClient.ConnectAsync(
            pipeName, TimeSpan.FromSeconds(10), timeout.Token);
        await connection;
        InstanceId instanceId = InstanceId.New();
        WorkerSessionId sessionId = WorkerSessionId.New();
        var worker = new AdapterControlChannel(workerPipe, instanceId.Value, sessionId.Value);
        var channel = new AdapterWorkerReadRangeClient(server, instanceId, sessionId);
        var stat = new AdapterWorkerStatClient(channel, instanceId, sessionId);
        Task<string?> pending = stat.StatAsync(new MirrorPulseWorkerStatRequest(instanceId, "missing.txt"),
            timeout.Token).AsTask();
        AdapterControlFrame command = await worker.ReadAsync(timeout.Token);
        Assert.AreEqual("Stat", command.MessageType);
        Task<byte[]> responseBytes = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("StatResult", command.RequestId, true, new { revision = (string?)null }, timeout.Token);
        ControlFrameEnvelope response = ControlFrameJsonCodec.Decode(await responseBytes);
        await stat.HandleResponseAsync(response);
        Assert.IsNull(await pending);
        stat.Close();
        channel.Close();
    }

    [TestMethod]
    public async Task UploadConflictCarriesRevisionPreconditionsAcrossThePipe()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string pipeName = $"mirrorpulse-upload-conflict-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connection = server.WaitForConnectionAsync(timeout.Token);
        await using AdapterNamedPipeClient pipe = await AdapterNamedPipeClient.ConnectAsync(pipeName, TimeSpan.FromSeconds(10), timeout.Token);
        await connection;
        InstanceId instance = InstanceId.New();
        WorkerSessionId session = WorkerSessionId.New();
        var worker = new AdapterControlChannel(pipe, instance.Value, session.Value);
        var channel = new AdapterWorkerReadRangeClient(server, instance, session);
        var client = new AdapterWorkerUploadClient(channel, instance, session);
        await using var bytes = new MemoryStream([1]);
        Task<string> pending = client.UploadAsync(new(instance, "file", "expected", bytes, 1, Guid.NewGuid()), timeout.Token).AsTask();
        AdapterControlFrame request = await worker.ReadAsync(timeout.Token);
        Task<byte[]> response = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("OperationError", request.RequestId, true,
            new { code = "RemoteConflict", expectedRevision = "expected", actualRevision = "changed" }, timeout.Token);
        await client.HandleResponseAsync(ControlFrameJsonCodec.Decode(await response));
        MirrorPulseWorkerMutationConflictException conflict = await Assert.ThrowsExactlyAsync<MirrorPulseWorkerMutationConflictException>(async () => await pending);
        Assert.AreEqual("expected", conflict.ExpectedRevision);
        Assert.AreEqual("changed", conflict.ActualRevision);
        client.Close();
        channel.Close();
    }
}

using System.IO.Pipes;
using System.Text;
using MirrorPulse.Adapter.Sdk;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;
using MirrorPulse.Core.Transport;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class AdapterWorkerReadRangeClientTests
{
    [TestMethod]
    public async Task SdkWorkerRangeFrameHydratesTheRequestedBytes()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string pipeName = $"mirrorpulse-range-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connection = server.WaitForConnectionAsync(timeout.Token);
        await using AdapterNamedPipeClient workerPipe = await AdapterNamedPipeClient.ConnectAsync(
            pipeName, TimeSpan.FromSeconds(10), timeout.Token);
        await connection;
        InstanceId instanceId = InstanceId.New();
        WorkerSessionId sessionId = WorkerSessionId.New();
        var worker = new AdapterControlChannel(workerPipe, instanceId.Value, sessionId.Value);
        var host = new AdapterWorkerReadRangeClient(server, instanceId, sessionId);
        byte[] content = Encoding.UTF8.GetBytes("range-content");
        var request = new MirrorPulseWorkerReadRangeRequest(instanceId, "notes.txt",
            ReadOnlyMemory<byte>.Empty, 3, content.Length);

        Task<Stream> pending = host.ReadRangeAsync(request, timeout.Token).AsTask();
        AdapterControlFrame command = await worker.ReadAsync(timeout.Token);
        Assert.AreEqual("ReadRange", command.MessageType);
        Assert.AreEqual("notes.txt", command.Payload.GetProperty("path").GetString());
        Assert.AreEqual(3L, command.Payload.GetProperty("offset").GetInt64());
        Guid streamId = Guid.NewGuid();
        Task<byte[]> responseBytes = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("ReadRangeReady", command.RequestId, true,
            new { streamId, length = content.Length }, timeout.Token);
        ControlFrameEnvelope response = ControlFrameJsonCodec.Decode(await responseBytes);
        Task handling = host.HandleResponseAsync(response, timeout.Token).AsTask();
        await worker.SendChunkAsync(new AdapterBinaryChunk(command.RequestId, instanceId.Value,
            sessionId.Value, streamId, 3, content, true), timeout.Token);
        await handling;

        await using Stream result = await pending;
        byte[] received = new byte[content.Length];
        await result.ReadExactlyAsync(received);
        CollectionAssert.AreEqual(content, received);
        host.Close();
    }

    [TestMethod]
    public async Task MismatchedBinaryOffsetFailsThePendingRange()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        string pipeName = $"mirrorpulse-range-test-{Guid.NewGuid():N}";
        using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        Task connection = server.WaitForConnectionAsync(timeout.Token);
        await using AdapterNamedPipeClient workerPipe = await AdapterNamedPipeClient.ConnectAsync(
            pipeName, TimeSpan.FromSeconds(10), timeout.Token);
        await connection;
        InstanceId instanceId = InstanceId.New();
        WorkerSessionId sessionId = WorkerSessionId.New();
        var worker = new AdapterControlChannel(workerPipe, instanceId.Value, sessionId.Value);
        var host = new AdapterWorkerReadRangeClient(server, instanceId, sessionId);
        var request = new MirrorPulseWorkerReadRangeRequest(instanceId, "notes.txt",
            ReadOnlyMemory<byte>.Empty, 3, 1);

        Task<Stream> pending = host.ReadRangeAsync(request, timeout.Token).AsTask();
        AdapterControlFrame command = await worker.ReadAsync(timeout.Token);
        Guid streamId = Guid.NewGuid();
        Task<byte[]> responseBytes = LengthPrefixedFrameReader.ReadAsync(server, timeout.Token).AsTask();
        await worker.SendAsync("ReadRangeReady", command.RequestId, true,
            new { streamId, length = 1 }, timeout.Token);
        ControlFrameEnvelope response = ControlFrameJsonCodec.Decode(await responseBytes);
        Task handling = host.HandleResponseAsync(response, timeout.Token).AsTask();
        await worker.SendChunkAsync(new AdapterBinaryChunk(command.RequestId, instanceId.Value,
            sessionId.Value, streamId, 4, new byte[] { 42 }, true), timeout.Token);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await handling);
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await pending);
        host.Close();
    }
}

using System.Text.Json;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Common metadata and JSON payload carried by one Named Pipe control frame.
/// </summary>
public sealed record ControlFrameEnvelope
{
    public ControlFrameEnvelope(
        int protocolVersion,
        string messageType,
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        bool isResponse,
        JsonElement payload)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(protocolVersion, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A control frame request ID cannot be empty.", nameof(requestId));
        }

        ProtocolVersion = protocolVersion;
        MessageType = messageType;
        RequestId = requestId;
        InstanceId = instanceId;
        WorkerSessionId = workerSessionId;
        IsResponse = isResponse;
        Payload = payload.Clone();
    }

    public int ProtocolVersion { get; }

    public string MessageType { get; }

    public Guid RequestId { get; }

    public InstanceId InstanceId { get; }

    public WorkerSessionId WorkerSessionId { get; }

    public bool IsResponse { get; }

    public JsonElement Payload { get; }
}

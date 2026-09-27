namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Common identity shared by every control request and response.
/// </summary>
public abstract record ControlMessage
{
    protected ControlMessage(
        string messageType,
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        bool isResponse)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A control message request ID cannot be empty.", nameof(requestId));
        }

        MessageType = messageType;
        RequestId = requestId;
        InstanceId = instanceId;
        WorkerSessionId = workerSessionId;
        IsResponse = isResponse;
    }

    public string MessageType { get; }

    public Guid RequestId { get; }

    public InstanceId InstanceId { get; }

    public WorkerSessionId WorkerSessionId { get; }

    public bool IsResponse { get; }
}

/// <summary>
/// Base contract for a request sent to a Worker or MP service.
/// </summary>
public abstract record ControlRequest : ControlMessage
{
    protected ControlRequest(string messageType, Guid requestId, InstanceId instanceId, WorkerSessionId workerSessionId)
        : base(messageType, requestId, instanceId, workerSessionId, isResponse: false)
    {
    }
}

/// <summary>
/// Base contract for a response correlated with a control request.
/// </summary>
public abstract record ControlResponse : ControlMessage
{
    protected ControlResponse(string messageType, Guid requestId, InstanceId instanceId, WorkerSessionId workerSessionId)
        : base(messageType, requestId, instanceId, workerSessionId, isResponse: true)
    {
    }
}

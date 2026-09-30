using System.Text.Json;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Control.Contracts;

/// <summary>
/// A command request sent to the current-user Host control plane.
/// </summary>
public sealed record ControlRequestEnvelope
{
    public ControlRequestEnvelope(
        int protocolVersion,
        Guid requestId,
        string command,
        JsonElement arguments,
        string? clientVersion = null)
    {
        ValidateVersion(protocolVersion);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A control request ID is required.", nameof(requestId));
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            throw new ArgumentException("A control command is required.", nameof(command));
        }

        if (arguments.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new ArgumentException("Control arguments must be a JSON value.", nameof(arguments));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Command = command.Trim();
        Arguments = arguments.Clone();
        ClientVersion = string.IsNullOrWhiteSpace(clientVersion) ? null : clientVersion.Trim();
    }

    public int ProtocolVersion { get; }

    public Guid RequestId { get; }

    public string Command { get; }

    public JsonElement Arguments { get; }

    public string? ClientVersion { get; }

    internal static void ValidateVersion(int protocolVersion)
    {
        if (protocolVersion != MirrorPulseControlSchema.CurrentVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(protocolVersion),
                protocolVersion,
                "The control protocol version is not supported.");
        }
    }
}

/// <summary>
/// A response to one control request.
/// </summary>
public sealed record ControlResponseEnvelope
{
    public ControlResponseEnvelope(
        int protocolVersion,
        Guid requestId,
        bool succeeded,
        JsonElement? data = null,
        ControlError? error = null,
        string? operationId = null)
    {
        ControlRequestEnvelope.ValidateVersion(protocolVersion);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A control request ID is required.", nameof(requestId));
        }

        if (succeeded == (error is not null))
        {
            throw new ArgumentException("A response must contain either data or one error.", nameof(error));
        }

        if (data is { ValueKind: JsonValueKind.Undefined })
        {
            throw new ArgumentException("Response data must be a JSON value.", nameof(data));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Succeeded = succeeded;
        Data = data?.Clone();
        Error = error;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
    }

    public int ProtocolVersion { get; }

    public Guid RequestId { get; }

    public bool Succeeded { get; }

    public JsonElement? Data { get; }

    public ControlError? Error { get; }

    public string? OperationId { get; }

    public static ControlResponseEnvelope Success(Guid requestId, JsonElement? data = null, string? operationId = null) =>
        new(MirrorPulseControlSchema.CurrentVersion, requestId, true, data, null, operationId);

    public static ControlResponseEnvelope Failure(Guid requestId, ControlError error) =>
        new(MirrorPulseControlSchema.CurrentVersion, requestId, false, null, error);
}

/// <summary>
/// An event emitted while a command is running.
/// </summary>
public sealed record ControlEventEnvelope
{
    public ControlEventEnvelope(
        int protocolVersion,
        Guid requestId,
        long sequence,
        string eventType,
        JsonElement data,
        bool isTerminal = false)
    {
        ControlRequestEnvelope.ValidateVersion(protocolVersion);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("A control request ID is required.", nameof(requestId));
        }

        if (sequence < 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sequence);
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("An event type is required.", nameof(eventType));
        }

        if (data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new ArgumentException("Event data must be a JSON value.", nameof(data));
        }

        ProtocolVersion = protocolVersion;
        RequestId = requestId;
        Sequence = sequence;
        EventType = eventType.Trim();
        Data = data.Clone();
        IsTerminal = isTerminal;
    }

    public int ProtocolVersion { get; }

    public Guid RequestId { get; }

    public long Sequence { get; }

    public string EventType { get; }

    public JsonElement Data { get; }

    public bool IsTerminal { get; }
}

/// <summary>
/// A safe, serializable error returned by Host.
/// </summary>
public sealed record ControlError
{
    public ControlError(
        string code,
        string message,
        ErrorCategory category,
        bool retryable = false,
        string? diagnosticId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An error code is required.", nameof(code));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("An error message is required.", nameof(message));
        }

        Code = code.Trim();
        Message = message.Trim();
        Category = category;
        Retryable = retryable;
        DiagnosticId = string.IsNullOrWhiteSpace(diagnosticId) ? null : diagnosticId.Trim();
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorCategory Category { get; }

    public bool Retryable { get; }

    public string? DiagnosticId { get; }
}

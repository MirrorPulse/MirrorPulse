namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Classifies a failure returned across an MP or Worker boundary.
/// </summary>
public enum ErrorCategory
{
    Unknown,
    Validation,
    Authentication,
    Authorization,
    Network,
    Conflict,
    Unsupported,
    Native,
    Protocol,
    Storage,
    Cancelled
}

/// <summary>
/// A user-visible or diagnostic message associated with an operation.
/// </summary>
public sealed record Diagnostic
{
    public Diagnostic(string code, string message, DiagnosticSeverity severity, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Severity = severity;
        Detail = detail;
    }

    public string Code { get; }

    public string Message { get; }

    public DiagnosticSeverity Severity { get; }

    public string? Detail { get; }
}

/// <summary>
/// Severity assigned to a diagnostic message.
/// </summary>
public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error
}

/// <summary>
/// Structured error information shared by local and Worker operations.
/// </summary>
public sealed record ErrorInfo
{
    public ErrorInfo(
        string code,
        string message,
        ErrorCategory category,
        bool retryable = false,
        bool authRequired = false,
        bool conflict = false,
        bool unsupported = false,
        string? nativeError = null,
        string? diagnosticId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        Code = code;
        Message = message;
        Category = category;
        Retryable = retryable;
        AuthRequired = authRequired;
        Conflict = conflict;
        Unsupported = unsupported;
        NativeError = nativeError;
        DiagnosticId = diagnosticId;
    }

    public string Code { get; }

    public string Message { get; }

    public ErrorCategory Category { get; }

    public bool Retryable { get; }

    public bool AuthRequired { get; }

    public bool Conflict { get; }

    public bool Unsupported { get; }

    public string? NativeError { get; }

    public string? DiagnosticId { get; }
}

/// <summary>
/// Represents either a successful value or one structured error.
/// </summary>
public sealed class Result<T>
{
    private readonly T? value;
    private readonly ErrorInfo? error;

    internal Result(T value)
    {
        this.value = value;
        IsSuccess = true;
    }

    internal Result(ErrorInfo error)
    {
        this.error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("A failed result has no value.");

    public ErrorInfo? Error => error;

}

/// <summary>
/// Factory methods for successful and failed operation results.
/// </summary>
public static class Result
{
    public static Result<T> Success<T>(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return new Result<T>(value);
    }

    public static Result<T> Failure<T>(ErrorInfo error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
}

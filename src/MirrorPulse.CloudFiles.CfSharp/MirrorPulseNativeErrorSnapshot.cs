using System.Text.Json;
using CfSharp;

namespace MirrorPulse.CloudFiles.CfSharp;

public sealed record MirrorPulseNativeErrorSnapshot(
    string ExceptionType,
    string Message,
    string? Operation,
    string? Path,
    int? Win32ErrorCode,
    int HResult,
    DateTimeOffset CapturedAt)
{
    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static MirrorPulseNativeErrorSnapshot Capture(
        Exception exception,
        DateTimeOffset? capturedAt = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        var native = exception as CloudFilesException;
        return new(
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            native?.Operation,
            native?.Path,
            native?.Win32ErrorCode,
            exception.HResult,
            capturedAt ?? DateTimeOffset.UtcNow);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };
}

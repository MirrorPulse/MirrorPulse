namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Privacy settings for the first product version. MP keeps diagnostics local and performs no telemetry upload.
/// </summary>
public sealed record PrivacySettings
{
    public PrivacySettings(bool collectUsageData = false, bool allowDiagnosticUpload = false)
    {
        if (collectUsageData || allowDiagnosticUpload)
        {
            throw new ArgumentException("This MirrorPulse version does not collect usage data or upload diagnostics.");
        }

        CollectUsageData = collectUsageData;
        AllowDiagnosticUpload = allowDiagnosticUpload;
    }

    public bool CollectUsageData { get; }

    public bool AllowDiagnosticUpload { get; }

    public bool IsLocalOnly => !CollectUsageData && !AllowDiagnosticUpload;
}

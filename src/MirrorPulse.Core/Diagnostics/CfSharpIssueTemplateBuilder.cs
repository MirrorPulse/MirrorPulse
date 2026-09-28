namespace MirrorPulse.Core.Diagnostics;

public sealed record CfSharpIssueReport(
    string Version,
    string Reproduction,
    string Expected,
    string Actual,
    string NativeError,
    string Impact,
    string Workaround,
    string Status);

/// <summary>
/// Produces a consistent Markdown report for suspected CfSharp preview regressions.
/// </summary>
public static class CfSharpIssueTemplateBuilder
{
    public static string Build(CfSharpIssueReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Reproduction);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Expected);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Actual);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Impact);
        ArgumentException.ThrowIfNullOrWhiteSpace(report.Status);

        return string.Join(
            Environment.NewLine,
            "# CfSharp preview issue",
            string.Empty,
            "## Version",
            string.Empty,
            report.Version.Trim(),
            string.Empty,
            "## Reproduction",
            string.Empty,
            report.Reproduction.Trim(),
            string.Empty,
            "## Expected",
            string.Empty,
            report.Expected.Trim(),
            string.Empty,
            "## Actual",
            string.Empty,
            report.Actual.Trim(),
            string.Empty,
            "## Native error",
            string.Empty,
            NormalizeOptional(report.NativeError),
            string.Empty,
            "## Impact",
            string.Empty,
            report.Impact.Trim(),
            string.Empty,
            "## Workaround",
            string.Empty,
            NormalizeOptional(report.Workaround),
            string.Empty,
            "## Status",
            string.Empty,
            report.Status.Trim());
    }

    private static string NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not captured." : value.Trim();
}

using System.Text.Json.Serialization;
using MirrorPulse.Core.Conflicts;

namespace MirrorPulse.Control.Contracts;

/// <summary>
/// Marker for a command argument object accepted by Host.
/// </summary>
public interface IMirrorPulseControlArguments;

/// <summary>
/// Marks a property that must be redacted by diagnostics and logging layers.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class MirrorPulseSensitiveDataAttribute : Attribute;

public sealed record ControlEmptyArguments : IMirrorPulseControlArguments;

public sealed record HostLifecycleArguments(bool Force = false) : IMirrorPulseControlArguments;

public sealed record AdapterInstallArguments(string PackagePath) : IMirrorPulseControlArguments;

public sealed record AdapterRemoveArguments(string AdapterId, bool Purge = false) : IMirrorPulseControlArguments;

public sealed record InstanceListArguments(int? Limit = null, string? Cursor = null) : IMirrorPulseControlArguments;

public sealed record InstanceCreateArguments(
    string InstallId,
    string DisplayName,
    IReadOnlyDictionary<string, string> Configuration,
    IReadOnlyDictionary<string, string> RootLabels,
    [property: MirrorPulseSensitiveData] string? Secret,
    bool Enabled) : IMirrorPulseControlArguments;

public sealed record InstanceIdArguments(string InstanceId) : IMirrorPulseControlArguments;

public sealed record InstanceEnableArguments(string InstanceId, bool Enabled) : IMirrorPulseControlArguments;

public sealed record InstanceSelectVersionArguments(string InstanceId, string InstallId) : IMirrorPulseControlArguments;

public sealed record ConflictListArguments(int? Limit = null, string? Cursor = null) : IMirrorPulseControlArguments;

public sealed record ConflictSnoozeArguments(Guid ConflictId) : IMirrorPulseControlArguments;

public sealed record ConflictResolveArguments(
    Guid ConflictId,
    MirrorPulseConflictAction Action,
    string? PreservedPath = null) : IMirrorPulseControlArguments;

public sealed record OperationIdArguments(string OperationId) : IMirrorPulseControlArguments;

public sealed record SettingsSetArguments(
    [property: MirrorPulseSensitiveData] IReadOnlyDictionary<string, string> Values) : IMirrorPulseControlArguments;

public sealed record DiagnosticsArguments(bool IncludeLogs = false) : IMirrorPulseControlArguments;

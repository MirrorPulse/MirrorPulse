namespace MirrorPulse.Core.Contracts;

/// <summary>Form input that an Adapter declares for each configured instance.</summary>
public enum AdapterConfigurationFieldKind
{
    Text,
    Choice,
    Toggle,
    Secret,
}

/// <summary>
/// Non-executable form metadata. Secret values are stored by MirrorPulse and supplied to the
/// Worker only through the reserved credentialReference configuration key.
/// </summary>
public sealed record AdapterConfigurationField(
    string Key,
    string Label,
    AdapterConfigurationFieldKind Kind,
    bool Required,
    string? DefaultValue,
    IReadOnlyList<string> Options);

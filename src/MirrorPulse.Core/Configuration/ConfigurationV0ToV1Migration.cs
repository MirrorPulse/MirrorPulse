using System.Text.Json.Nodes;

namespace MirrorPulse.Core.Configuration;

/// <summary>
/// Adds the v1 sync-root display name to the initial persisted configuration shape.
/// </summary>
public sealed class ConfigurationV0ToV1Migration : IConfigurationSchemaMigration
{
    public int FromVersion => 0;

    public int ToVersion => 1;

    public JsonObject Apply(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.TryAdd("syncRootDisplayName", "MirrorPulse");
        document["schemaVersion"] = ToVersion;
        return document;
    }
}

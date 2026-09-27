using System.Text.Json.Nodes;

namespace MirrorPulse.Core.Configuration;

/// <summary>
/// One forward-only transformation between two persisted configuration schemas.
/// </summary>
public interface IConfigurationSchemaMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonObject Apply(JsonObject document);
}

public sealed record ConfigurationMigrationResult(
    JsonObject Document,
    int OriginalVersion,
    int FinalVersion,
    bool Changed);

/// <summary>
/// Applies a deterministic chain of configuration migrations without writing files.
/// </summary>
public sealed class ConfigurationSchemaMigrator
{
    private readonly Dictionary<int, IConfigurationSchemaMigration> _migrations;

    public ConfigurationSchemaMigrator(int currentVersion, IEnumerable<IConfigurationSchemaMigration> migrations)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion), "The current schema version must be positive.");
        }

        ArgumentNullException.ThrowIfNull(migrations);
        var materialized = migrations.ToArray();
        if (materialized.Any(migration => migration.FromVersion < 0 || migration.ToVersion <= migration.FromVersion))
        {
            throw new ArgumentException("Schema migrations must move forward.", nameof(migrations));
        }

        if (materialized.GroupBy(migration => migration.FromVersion).Any(group => group.Count() > 1))
        {
            throw new ArgumentException("Each source schema version can have only one migration.", nameof(migrations));
        }

        CurrentVersion = currentVersion;
        _migrations = materialized.ToDictionary(migration => migration.FromVersion);
    }

    public int CurrentVersion { get; }

    public ConfigurationMigrationResult Migrate(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var originalVersion = ReadVersion(document);
        if (originalVersion > CurrentVersion)
        {
            throw new InvalidDataException($"Unsupported configuration schema version: {originalVersion}.");
        }

        var current = document.DeepClone().AsObject();
        var version = originalVersion;
        while (version < CurrentVersion)
        {
            if (!_migrations.TryGetValue(version, out var migration))
            {
                throw new InvalidDataException($"No configuration migration is registered for schema version: {version}.");
            }

            current = migration.Apply(current) ?? throw new InvalidDataException("A configuration migration returned no document.");
            var migratedVersion = ReadVersion(current);
            if (migratedVersion != migration.ToVersion || migratedVersion <= version)
            {
                throw new InvalidDataException("A configuration migration returned an invalid schema version.");
            }

            version = migratedVersion;
        }

        return new ConfigurationMigrationResult(current, originalVersion, version, version != originalVersion);
    }

    private static int ReadVersion(JsonObject document)
    {
        try
        {
            return document["schemaVersion"]?.GetValue<int>()
                ?? throw new InvalidDataException("The configuration document does not declare schemaVersion.");
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The configuration schemaVersion is not an integer.", exception);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The configuration schemaVersion is not an integer.", exception);
        }
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Configuration;

/// <summary>
/// Stores current-user MP configuration as an atomically replaced JSON file.
/// </summary>
public sealed class MirrorPulseConfigurationStore
{
    private static readonly ConfigurationSchemaMigrator SchemaMigrator = new(
        MirrorPulseConfiguration.CurrentSchemaVersion,
        [new ConfigurationV0ToV1Migration()]);

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly string _filePath;

    public MirrorPulseConfigurationStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        _filePath = Path.GetFullPath(filePath);
    }

    public string FilePath => _filePath;

    public async Task SaveAsync(MirrorPulseConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var directory = Path.GetDirectoryName(_filePath) ?? throw new InvalidOperationException("The configuration path has no directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
        var document = new ConfigurationDocument(
            configuration.SchemaVersion,
            configuration.Locale,
            configuration.DeveloperMode,
            configuration.StartWithWindows,
            configuration.EnabledInstallations.Select(installation => installation.ToString()).ToArray(),
            configuration.SyncRootDisplayName);

        try
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(document, SerializerOptions);
            await File.WriteAllBytesAsync(temporaryPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, _filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<MirrorPulseConfiguration?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var source = JsonNode.Parse(json)?.AsObject()
                ?? throw new InvalidDataException("The configuration document is empty.");
            var migrated = SchemaMigrator.Migrate(source).Document;
            var document = JsonSerializer.Deserialize<ConfigurationDocument>(migrated.ToJsonString(), SerializerOptions)
                ?? throw new InvalidDataException("The configuration document is empty.");
            if (document.SchemaVersion != MirrorPulseConfiguration.CurrentSchemaVersion)
            {
                throw new InvalidDataException($"Unsupported configuration schema version: {document.SchemaVersion}.");
            }

            var installations = document.EnabledInstallations.Select(InstallId.Parse).ToArray();
            return new MirrorPulseConfiguration(
                document.SchemaVersion,
                document.Locale,
                document.DeveloperMode,
                document.StartWithWindows,
                installations,
                document.SyncRootDisplayName);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The configuration document is not valid JSON.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The configuration document contains invalid values.", exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The configuration document must be a JSON object.", exception);
        }
    }

    private sealed record ConfigurationDocument(
        int SchemaVersion,
        string Locale,
        bool DeveloperMode,
        bool StartWithWindows,
        string[] EnabledInstallations,
        string SyncRootDisplayName);
}

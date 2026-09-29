using System.Text.Json;
using Microsoft.Data.Sqlite;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.State;

public sealed partial class MirrorPulseProductCatalog
{
    /// <summary>
    /// Creates the first runnable instance for an installed Adapter and registers all of its
    /// first-level roots in the same product-catalog transaction.
    /// </summary>
    public async Task<AdapterInstance> CreateInstanceAsync(
        InstallId installId,
        string displayName,
        IReadOnlyDictionary<string, string> configuration,
        IReadOnlyList<string> credentialReferences,
        string fileCacheDirectory,
        string transferCacheDirectory,
        bool enabled = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(credentialReferences);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileCacheDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(transferCacheDirectory);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand query = _connection.CreateCommand();
            query.CommandText = "SELECT payload FROM adapter_topology WHERE id = 1;";
            string? payload = (string?)await query.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            MirrorPulseAdapterTopology current = payload is null ? new([], [], []) : DeserializeTopology(payload);
            InstalledAdapter installation = current.Installations.SingleOrDefault(item => item.InstallId == installId)
                ?? throw new FileNotFoundException("The selected Adapter installation is not registered.");

            int existingInstances = current.Instances.Count(item => item.AdapterId == installation.AdapterId);
            int? maximumInstances = installation.Manifest.InstancePolicy.MaximumInstances;
            if (maximumInstances is not null && existingInstances >= maximumInstances.Value)
            {
                throw new InvalidDataException("The Adapter instance limit has been reached.");
            }

            int? maximumRoots = installation.Manifest.InstancePolicy.MaximumRootDefinitions;
            if (maximumRoots is not null && installation.Manifest.RootDefinitions.Count > maximumRoots.Value)
            {
                throw new InvalidDataException("The Adapter root-definition limit has been exceeded.");
            }

            InstanceId instanceId = InstanceId.New();
            AdapterInstance instance = new(
                installation.AdapterId,
                installation.InstallId,
                instanceId,
                displayName,
                configuration,
                credentialReferences,
                Path.GetFullPath(fileCacheDirectory),
                Path.GetFullPath(transferCacheDirectory),
                enabled,
                enabled ? AdapterLifecycleState.Enabled : AdapterLifecycleState.Disabled,
                null,
                DateTimeOffset.UtcNow);
            RootRegistration[] roots = AdapterRootRegistrationMapper.MapAll(
                installation.AdapterId,
                instanceId,
                installation.Manifest.RootDefinitions,
                enabled ? RootRegistrationState.Active : RootRegistrationState.Disabled).ToArray();
            var next = new MirrorPulseAdapterTopology(
                [.. current.Installations],
                [.. current.Instances, instance],
                [.. current.Roots, .. roots]);
            ValidateTopology(next);

            await using SqliteCommand update = _connection.CreateCommand();
            update.CommandText = """
                INSERT INTO adapter_topology (id, payload) VALUES (1, $payload)
                ON CONFLICT(id) DO UPDATE SET payload = excluded.payload;
                """;
            update.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(next, TopologyJsonOptions));
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(instance.FileCacheDirectory);
            Directory.CreateDirectory(instance.TransferCacheDirectory);
            return instance;
        }
        finally
        {
            _gate.Release();
        }
    }
}

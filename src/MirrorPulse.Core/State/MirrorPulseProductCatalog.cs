using Microsoft.Data.Sqlite;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Sync;

namespace MirrorPulse.Core.State;

public sealed record MirrorPulseWorkerRequestRecord(
    Guid OperationId,
    InstanceId InstanceId,
    byte[] Fingerprint,
    int Attempt,
    DateTimeOffset? NextAttemptAt);

public sealed record MirrorPulseUserCommandRecord(
    Guid CommandId,
    string Action,
    string TargetId,
    string State);

/// <summary>
/// MP's separate product catalog. CfSharp owns Cloud Files journal, batch, conflict and checkpoint
/// tables in its own database; this catalog tracks Worker idempotency, user intent, and
/// non-authoritative conflict UI projections.
/// </summary>
public sealed class MirrorPulseProductCatalog : IAsyncDisposable
{
    private readonly FileStream _owner;
    private readonly SqliteConnection _connection;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    private MirrorPulseProductCatalog(FileStream owner, SqliteConnection connection)
    {
        _owner = owner;
        _connection = connection;
    }

    public static async Task<MirrorPulseProductCatalog> OpenAsync(
        MirrorPulseStoragePaths paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        string path = paths.ProductCatalogDatabasePath;
        if (string.Equals(path, paths.CfSharpStateDatabasePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The MP product catalog must be separate from CfSharp state.");
        }

        string directory = Path.GetDirectoryName(path)
            ?? throw new InvalidDataException("The product catalog has no parent directory.");
        Directory.CreateDirectory(directory);
        FileStream owner = new(
            path + ".owner",
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.DeleteOnClose);
        SqliteConnection? connection = null;
        try
        {
            var settings = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Private,
                Pooling = false,
                ForeignKeys = true,
            };
            connection = new SqliteConnection(settings.ToString());
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using (SqliteCommand mode = connection.CreateCommand())
            {
                mode.CommandText = "PRAGMA journal_mode=WAL;";
                object? result = await mode.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (!string.Equals(result?.ToString(), "wal", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The MP product catalog could not enable SQLite WAL mode.");
                }
            }

            await using (SqliteCommand version = connection.CreateCommand())
            {
                version.CommandText = "PRAGMA user_version;";
                long currentVersion = (long)(await version.ExecuteScalarAsync(cancellationToken)
                    .ConfigureAwait(false) ?? 0L);
                if (currentVersion > 2)
                {
                    throw new InvalidDataException("The MP product catalog schema is newer than this Host supports.");
                }
            }

            await using (SqliteCommand schema = connection.CreateCommand())
            {
                schema.CommandText = """
                    CREATE TABLE IF NOT EXISTS worker_requests (
                        operation_id TEXT PRIMARY KEY,
                        instance_id TEXT NOT NULL,
                        fingerprint BLOB NOT NULL,
                        attempt INTEGER NOT NULL DEFAULT 0,
                        next_attempt_utc TEXT NULL
                    );
                    CREATE TABLE IF NOT EXISTS user_commands (
                        command_id TEXT PRIMARY KEY,
                        action TEXT NOT NULL,
                        target_id TEXT NOT NULL,
                        state TEXT NOT NULL
                    );
                    CREATE TABLE IF NOT EXISTS remote_conflict_projections (
                        conflict_id TEXT PRIMARY KEY,
                        instance_id TEXT NOT NULL,
                        change_id TEXT NOT NULL,
                        relative_path TEXT NOT NULL,
                        reason INTEGER NOT NULL,
                        version_comparison INTEGER NOT NULL,
                        local_revision TEXT NULL,
                        remote_revision TEXT NULL,
                        detected_utc TEXT NOT NULL
                    );
                    PRAGMA user_version=2;
                    """;
                await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return new(owner, connection);
        }
        catch
        {
            if (connection is not null)
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }

            owner.Dispose();
            throw;
        }
    }

    /// <summary>Returns false for an identical replay and rejects reuse with a different payload.</summary>
    public async Task<bool> TryRecordWorkerRequestAsync(
        Guid operationId,
        InstanceId instanceId,
        ReadOnlyMemory<byte> fingerprint,
        CancellationToken cancellationToken = default)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("The Worker operation ID cannot be empty.", nameof(operationId));
        }

        if (fingerprint.Length != 32)
        {
            throw new ArgumentException("A Worker request fingerprint must be a SHA-256 digest.", nameof(fingerprint));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand insert = _connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO worker_requests (operation_id, instance_id, fingerprint)
                VALUES ($operation, $instance, $fingerprint)
                ON CONFLICT(operation_id) DO NOTHING;
                """;
            insert.Parameters.AddWithValue("$operation", operationId.ToString("D"));
            insert.Parameters.AddWithValue("$instance", instanceId.ToString());
            insert.Parameters.AddWithValue("$fingerprint", fingerprint.ToArray());
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
            {
                return true;
            }

            MirrorPulseWorkerRequestRecord existing = await ReadWorkerRequestCoreAsync(operationId, cancellationToken)
                .ConfigureAwait(false) ?? throw new InvalidDataException("The existing Worker request disappeared.");
            if (existing.InstanceId != instanceId || !existing.Fingerprint.AsSpan().SequenceEqual(fingerprint.Span))
            {
                throw new InvalidDataException("A Worker operation ID was reused with a different request.");
            }

            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MirrorPulseWorkerRequestRecord?> ReadWorkerRequestAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await ReadWorkerRequestCoreAsync(operationId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveUserCommandAsync(
        MirrorPulseUserCommandRecord command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.CommandId == Guid.Empty)
        {
            throw new ArgumentException("The user command ID cannot be empty.", nameof(command));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.Action);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.TargetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.State);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand insert = _connection.CreateCommand();
            insert.CommandText = """
                INSERT INTO user_commands (command_id, action, target_id, state)
                VALUES ($id, $action, $target, $state)
                ON CONFLICT(command_id) DO UPDATE SET state = excluded.state
                WHERE user_commands.action = excluded.action
                  AND user_commands.target_id = excluded.target_id;
                """;
            insert.Parameters.AddWithValue("$id", command.CommandId.ToString("D"));
            insert.Parameters.AddWithValue("$action", command.Action);
            insert.Parameters.AddWithValue("$target", command.TargetId);
            insert.Parameters.AddWithValue("$state", command.State);
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 0)
            {
                throw new InvalidDataException("A user command ID was reused with different intent.");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MirrorPulseUserCommandRecord?> ReadUserCommandAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand query = _connection.CreateCommand();
            query.CommandText = "SELECT action, target_id, state FROM user_commands WHERE command_id = $id;";
            query.Parameters.AddWithValue("$id", commandId.ToString("D"));
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new(commandId, reader.GetString(0), reader.GetString(1), reader.GetString(2))
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stores UI metadata for a conflict whose authority remains in CfSharp.</summary>
    public async Task SaveRemoteConflictProjectionAsync(
        MirrorPulseConflictRecord conflict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        if (conflict.Source != MirrorPulseConflictSource.CfSharpRemote || !conflict.IsPending)
        {
            throw new ArgumentException("Only pending CfSharp remote conflicts can be projected.", nameof(conflict));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO remote_conflict_projections
                    (conflict_id, instance_id, change_id, relative_path, reason,
                     version_comparison, local_revision, remote_revision, detected_utc)
                VALUES ($id, $instance, $change, $path, $reason, $comparison, $local, $remote, $detected)
                ON CONFLICT(conflict_id) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$id", conflict.ConflictId.ToString("D"));
            command.Parameters.AddWithValue("$instance", conflict.InstanceId.ToString());
            command.Parameters.AddWithValue("$change", conflict.ChangeId);
            command.Parameters.AddWithValue("$path", conflict.RelativePath);
            command.Parameters.AddWithValue("$reason", (int)conflict.Reason);
            command.Parameters.AddWithValue("$comparison", (int)conflict.VersionComparison);
            command.Parameters.AddWithValue("$local", (object?)conflict.LocalRevision ?? DBNull.Value);
            command.Parameters.AddWithValue("$remote", (object?)conflict.RemoteRevision ?? DBNull.Value);
            command.Parameters.AddWithValue("$detected", conflict.DetectedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<MirrorPulseConflictRecord>> ReadRemoteConflictProjectionsAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand query = _connection.CreateCommand();
            query.CommandText = """
                SELECT conflict_id, instance_id, change_id, relative_path, reason,
                       version_comparison, local_revision, remote_revision, detected_utc
                FROM remote_conflict_projections ORDER BY detected_utc, conflict_id;
                """;
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var records = new List<MirrorPulseConflictRecord>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                records.Add(new MirrorPulseConflictRecord(
                    Guid.Parse(reader.GetString(0)),
                    InstanceId.Parse(reader.GetString(1)),
                    reader.GetString(2),
                    reader.GetString(3),
                    (MirrorPulseConflictReason)reader.GetInt32(4),
                    (MirrorPulseVersionComparison)reader.GetInt32(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.IsDBNull(7) ? null : reader.GetString(7),
                    DateTimeOffset.Parse(reader.GetString(8), System.Globalization.CultureInfo.InvariantCulture),
                    source: MirrorPulseConflictSource.CfSharpRemote));
            }

            return records;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await _connection.DisposeAsync().ConfigureAwait(false);
            _owner.Dispose();
            _disposed = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<MirrorPulseWorkerRequestRecord?> ReadWorkerRequestCoreAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        await using SqliteCommand query = _connection.CreateCommand();
        query.CommandText = """
            SELECT instance_id, fingerprint, attempt, next_attempt_utc
            FROM worker_requests WHERE operation_id = $operation;
            """;
        query.Parameters.AddWithValue("$operation", operationId.ToString("D"));
        await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new(
            operationId,
            InstanceId.Parse(reader.GetString(0)),
            (byte[])reader.GetValue(1),
            reader.GetInt32(2),
            reader.IsDBNull(3) ? null : DateTimeOffset.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture));
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}

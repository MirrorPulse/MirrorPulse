using Microsoft.Data.Sqlite;

namespace MirrorPulse.Core.State;

public sealed partial class MirrorPulseProductCatalog
{
    /// <summary>Persists whether a pending conflict should stop interrupting the user.</summary>
    public async Task SetConflictSnoozedAsync(
        Guid conflictId,
        bool snoozed,
        CancellationToken cancellationToken = default)
    {
        if (conflictId == Guid.Empty)
        {
            throw new ArgumentException("The conflict ID cannot be empty.", nameof(conflictId));
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand command = _connection.CreateCommand();
            command.CommandText = snoozed
                ? """
                    INSERT INTO notification_snoozes (conflict_id, snoozed_utc)
                    VALUES ($id, $at) ON CONFLICT(conflict_id) DO NOTHING;
                    """
                : "DELETE FROM notification_snoozes WHERE conflict_id = $id;";
            command.Parameters.AddWithValue("$id", conflictId.ToString("D"));
            if (snoozed)
            {
                command.Parameters.AddWithValue("$at", DateTimeOffset.UtcNow.ToString("O",
                    System.Globalization.CultureInfo.InvariantCulture));
            }

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlySet<Guid>> ReadSnoozedConflictIdsAsync(
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using SqliteCommand query = _connection.CreateCommand();
            query.CommandText = "SELECT conflict_id FROM notification_snoozes;";
            await using SqliteDataReader reader = await query.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            var ids = new HashSet<Guid>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(Guid.Parse(reader.GetString(0)));
            }

            return ids;
        }
        finally
        {
            _gate.Release();
        }
    }
}

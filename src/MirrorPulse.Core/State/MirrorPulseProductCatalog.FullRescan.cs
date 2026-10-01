using System.Globalization;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.State;

public enum MirrorPulseFullRescanPhase { Requested, Running, Acknowledging }
public sealed record MirrorPulseFullRescanCheckpoint(Guid Generation, MirrorPulseFullRescanPhase Phase, DateTimeOffset UpdatedAt);

public sealed partial class MirrorPulseProductCatalog
{
    public async Task RequireRootRescanAsync(RootId rootId, CancellationToken cancellationToken = default)
    {
        if (rootId.Value == Guid.Empty) throw new ArgumentException("The rescan root must not be empty.", nameof(rootId));
        await SetRootRescanAsync(rootId, true, cancellationToken).ConfigureAwait(false);
    }

    public Task CompleteRootRescanAsync(RootId rootId, CancellationToken cancellationToken = default) => SetRootRescanAsync(rootId, false, cancellationToken);

    public async Task<IReadOnlyList<RootId>> ReadDeferredRescanRootsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using var command = _connection.CreateCommand();
            command.CommandText = "SELECT root_id FROM deferred_rescan_roots ORDER BY root_id;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            var roots = new List<RootId>();
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) roots.Add(RootId.Parse(reader.GetString(0)));
            return roots.AsReadOnly();
        }
        finally { _gate.Release(); }
    }

    private async Task SetRootRescanAsync(RootId rootId, bool required, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using var command = _connection.CreateCommand();
            command.CommandText = required ? "INSERT INTO deferred_rescan_roots (root_id) VALUES ($root) ON CONFLICT(root_id) DO NOTHING;" :
                "DELETE FROM deferred_rescan_roots WHERE root_id=$root;";
            command.Parameters.AddWithValue("$root", rootId.ToString());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<MirrorPulseFullRescanCheckpoint> BeginFullRescanAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using var command = _connection.CreateCommand();
            command.CommandText = "INSERT INTO full_rescan_checkpoint (singleton,generation,phase,updated_utc) VALUES (1,$generation,$phase,$updated) ON CONFLICT(singleton) DO NOTHING;";
            command.Parameters.AddWithValue("$generation", Guid.NewGuid().ToString("D"));
            command.Parameters.AddWithValue("$phase", (int)MirrorPulseFullRescanPhase.Requested);
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return await ReadFullRescanCoreAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("The full rescan checkpoint disappeared.");
        }
        finally { _gate.Release(); }
    }

    public async Task<MirrorPulseFullRescanCheckpoint?> ReadFullRescanAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { ThrowIfDisposed(); return await ReadFullRescanCoreAsync(cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task UpdateFullRescanAsync(Guid generation, MirrorPulseFullRescanPhase phase, CancellationToken cancellationToken = default)
    {
        if (generation == Guid.Empty || !Enum.IsDefined(phase)) throw new ArgumentException("The full rescan checkpoint is invalid.");
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE full_rescan_checkpoint SET phase=$phase,updated_utc=$updated WHERE singleton=1 AND generation=$generation;";
            command.Parameters.AddWithValue("$phase", (int)phase);
            command.Parameters.AddWithValue("$updated", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$generation", generation.ToString("D"));
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("The full rescan generation changed.");
        }
        finally { _gate.Release(); }
    }

    public async Task CompleteFullRescanAsync(Guid generation, CancellationToken cancellationToken = default)
    {
        if (generation == Guid.Empty) throw new ArgumentException("The full rescan generation must not be empty.", nameof(generation));
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            await using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM full_rescan_checkpoint WHERE singleton=1 AND generation=$generation;";
            command.Parameters.AddWithValue("$generation", generation.ToString("D"));
            if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("The full rescan generation changed.");
        }
        finally { _gate.Release(); }
    }

    private async Task<MirrorPulseFullRescanCheckpoint?> ReadFullRescanCoreAsync(CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText = "SELECT generation,phase,updated_utc FROM full_rescan_checkpoint WHERE singleton=1;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        Guid generation = Guid.Parse(reader.GetString(0));
        var phase = (MirrorPulseFullRescanPhase)reader.GetInt32(1);
        if (generation == Guid.Empty || !Enum.IsDefined(phase)) throw new InvalidDataException("The full rescan checkpoint is invalid.");
        return new(generation, phase, DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture));
    }
}

using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Conflicts;

/// <summary>
/// Thread-safe in-process conflict catalog used by the Host and conflict center UI.
/// </summary>
public sealed class MirrorPulseConflictCenter
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, MirrorPulseConflictRecord> _records = [];

    public void Upsert(MirrorPulseConflictRecord conflict)
    {
        ArgumentNullException.ThrowIfNull(conflict);
        lock (_gate)
        {
            _records[conflict.ConflictId] = conflict;
        }
    }

    public IReadOnlyList<MirrorPulseConflictRecord> Query(
        InstanceId? instanceId = null,
        bool pendingOnly = true)
    {
        lock (_gate)
        {
            var records = _records.Values
                .Where(record => instanceId is null || record.InstanceId == instanceId.Value)
                .Where(record => !pendingOnly || record.IsPending)
                .OrderBy(record => record.DetectedAt)
                .ThenBy(record => record.ConflictId)
                .ToArray();
            return new ReadOnlyCollection<MirrorPulseConflictRecord>(records);
        }
    }
}

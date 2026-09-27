using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

public sealed record AdapterQuarantineRecord(
    InstallId InstallId,
    string Reason,
    DateTimeOffset QuarantinedAt,
    int FailureCount);

/// <summary>
/// Prevents repeatedly starting an installation that failed package or runtime validation.
/// </summary>
public sealed class AdapterQuarantineRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<InstallId, AdapterQuarantineRecord> _records = new();

    public AdapterQuarantineRecord Quarantine(InstallId installId, string reason, DateTimeOffset at)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            var failureCount = _records.TryGetValue(installId, out var previous) ? previous.FailureCount + 1 : 1;
            var record = new AdapterQuarantineRecord(installId, reason.Trim(), at, failureCount);
            _records[installId] = record;
            return record;
        }
    }

    public bool IsQuarantined(InstallId installId)
    {
        lock (_gate)
        {
            return _records.ContainsKey(installId);
        }
    }

    public bool TryGet(InstallId installId, out AdapterQuarantineRecord? record)
    {
        lock (_gate)
        {
            return _records.TryGetValue(installId, out record);
        }
    }

    public bool Clear(InstallId installId)
    {
        lock (_gate)
        {
            return _records.Remove(installId);
        }
    }

    public IReadOnlyDictionary<InstallId, AdapterQuarantineRecord> Snapshot()
    {
        lock (_gate)
        {
            return new ReadOnlyDictionary<InstallId, AdapterQuarantineRecord>(new Dictionary<InstallId, AdapterQuarantineRecord>(_records));
        }
    }
}

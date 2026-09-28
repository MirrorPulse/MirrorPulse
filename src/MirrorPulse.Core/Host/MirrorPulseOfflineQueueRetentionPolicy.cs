using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Host;

/// <summary>
/// Decides whether an Adapter instance may drain uploads while retaining its pending queue.
/// </summary>
public sealed record MirrorPulseAdapterQueueDisposition(
    bool WorkerMayRun,
    bool RetainPendingOperations,
    bool DrainPendingOperations);

public static class MirrorPulseOfflineQueueRetentionPolicy
{
    public static MirrorPulseAdapterQueueDisposition Resolve(
        AdapterActivationSnapshot activation,
        InstallId installId)
    {
        ArgumentNullException.ThrowIfNull(activation);
        if (activation.IsEnabled(installId))
        {
            return new(true, true, true);
        }

        return new(false, true, false);
    }
}

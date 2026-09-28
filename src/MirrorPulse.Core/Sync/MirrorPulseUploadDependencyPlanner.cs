namespace MirrorPulse.Core.Sync;

/// <summary>
/// Presents the ordered CfSharp journal as a Worker delivery plan. Sequence is owned by CfSharp;
/// MP does not persist another dependency graph or reorder operations by local timestamps.
/// </summary>
public static class MirrorPulseUploadDependencyPlanner
{
    public static IReadOnlyList<MirrorPulseWorkerChangeCommand> Plan(
        IEnumerable<MirrorPulseWorkerChangeCommand> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        MirrorPulseWorkerChangeCommand[] snapshot = commands.ToArray();
        if (snapshot.Any(command => command.OperationId == Guid.Empty || command.Sequence <= 0))
        {
            throw new InvalidDataException("Every Worker command requires a CfSharp operation ID and journal sequence.");
        }

        if (snapshot.Select(command => command.OperationId).Distinct().Count() != snapshot.Length ||
            snapshot.Select(command => command.Sequence).Distinct().Count() != snapshot.Length)
        {
            throw new InvalidDataException("A CfSharp journal page cannot repeat an operation ID or sequence.");
        }

        return Array.AsReadOnly(snapshot.OrderBy(command => command.Sequence).ToArray());
    }
}

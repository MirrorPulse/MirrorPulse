namespace MirrorPulse.Core.Sync;

/// <summary>
/// Produces a deterministic topological order for queued uploads.
/// </summary>
public static class MirrorPulseUploadDependencyPlanner
{
    public static IReadOnlyList<MirrorPulseQueuedUpload> Plan(
        IEnumerable<MirrorPulseQueuedUpload> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        var operationMap = operations.ToDictionary(operation => operation.OperationId);
        var indegree = operationMap.Keys.ToDictionary(operationId => operationId, _ => 0);
        var dependents = operationMap.Keys.ToDictionary(operationId => operationId, _ => new List<Guid>());

        foreach (var operation in operationMap.Values)
        {
            foreach (var dependency in operation.Dependencies)
            {
                if (dependency == operation.OperationId)
                {
                    throw new InvalidDataException("An upload operation cannot depend on itself.");
                }

                if (!operationMap.ContainsKey(dependency))
                {
                    throw new InvalidDataException(
                        $"Upload operation {operation.OperationId:D} depends on a missing operation.");
                }

                indegree[operation.OperationId]++;
                dependents[dependency].Add(operation.OperationId);
            }
        }

        var ready = new SortedSet<Guid>(CreateComparer(operationMap));
        foreach (var pair in indegree.Where(pair => pair.Value == 0))
        {
            ready.Add(pair.Key);
        }

        var ordered = new List<MirrorPulseQueuedUpload>(operationMap.Count);
        while (ready.Count > 0)
        {
            var operationId = ready.Min;
            ready.Remove(operationId);
            ordered.Add(operationMap[operationId]);

            foreach (var dependent in dependents[operationId])
            {
                indegree[dependent]--;
                if (indegree[dependent] == 0)
                {
                    ready.Add(dependent);
                }
            }
        }

        if (ordered.Count != operationMap.Count)
        {
            throw new InvalidDataException("The upload operation graph contains a dependency cycle.");
        }

        return ordered;
    }

    private static Comparer<Guid> CreateComparer(
        Dictionary<Guid, MirrorPulseQueuedUpload> operationMap) =>
        Comparer<Guid>.Create((left, right) =>
        {
            if (left == right)
            {
                return 0;
            }

            var comparison = operationMap[left].CreatedAt.CompareTo(operationMap[right].CreatedAt);
            return comparison != 0 ? comparison : left.CompareTo(right);
        });
}

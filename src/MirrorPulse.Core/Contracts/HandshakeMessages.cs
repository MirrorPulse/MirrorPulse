using System.Collections.ObjectModel;

namespace MirrorPulse.Core.Contracts;

/// <summary>
/// Worker hello request sent after the process connects to the Named Pipe.
/// </summary>
public sealed record HelloMessage : ControlRequest
{
    public HelloMessage(Guid requestId, InstanceId instanceId, WorkerProtocolOffer offer)
        : base("Hello", requestId, instanceId, GetSession(offer))
    {
        ArgumentNullException.ThrowIfNull(offer);
        Offer = offer;
    }

    public WorkerProtocolOffer Offer { get; }

    private static WorkerSessionId GetSession(WorkerProtocolOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);
        return offer.WorkerSessionId;
    }
}

/// <summary>
/// MP ready response that completes a successful Worker handshake.
/// </summary>
public sealed record ReadyMessage : ControlResponse
{
    public ReadyMessage(
        Guid requestId,
        InstanceId instanceId,
        WorkerSessionId workerSessionId,
        WorkerProtocolSelection selection,
        IReadOnlyList<RootRegistration> roots)
        : base("Ready", requestId, instanceId, workerSessionId)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(roots);
        Selection = selection;
        Roots = new ReadOnlyCollection<RootRegistration>(roots.ToArray());
    }

    public WorkerProtocolSelection Selection { get; }

    public IReadOnlyList<RootRegistration> Roots { get; }
}

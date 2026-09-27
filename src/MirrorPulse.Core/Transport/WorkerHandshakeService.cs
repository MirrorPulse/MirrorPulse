using System.Collections.ObjectModel;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Transport;

/// <summary>
/// Result of the MP side of a Worker Hello/Ready handshake.
/// </summary>
public sealed record HandshakeResult
{
    public HandshakeResult(WorkerProtocolSelection selection, ReadyMessage? ready)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.Accepted != (ready is not null))
        {
            throw new ArgumentException("An accepted handshake must have a Ready message and a rejected handshake must not.", nameof(ready));
        }

        Selection = selection;
        Ready = ready;
    }

    public WorkerProtocolSelection Selection { get; }

    public ReadyMessage? Ready { get; }
}

/// <summary>
/// Negotiates the highest common protocol version and returns the registered roots.
/// </summary>
public sealed class WorkerHandshakeService
{
    private readonly ProtocolVersionRange _supportedVersions;
    private readonly IReadOnlyList<RootRegistration> _roots;

    public WorkerHandshakeService(ProtocolVersionRange supportedVersions, IEnumerable<RootRegistration> roots)
    {
        ArgumentNullException.ThrowIfNull(supportedVersions);
        ArgumentNullException.ThrowIfNull(roots);
        if (supportedVersions.Minimum < 1 || supportedVersions.Minimum > supportedVersions.Maximum)
        {
            throw new ArgumentException("The host protocol version range is invalid.", nameof(supportedVersions));
        }

        _supportedVersions = supportedVersions;
        _roots = new ReadOnlyCollection<RootRegistration>(roots.ToArray());
    }

    public HandshakeResult HandleHello(HelloMessage hello)
    {
        ArgumentNullException.ThrowIfNull(hello);
        var offer = hello.Offer.SupportedVersions;
        var minimum = Math.Max(_supportedVersions.Minimum, offer.Minimum);
        var maximum = Math.Min(_supportedVersions.Maximum, offer.Maximum);
        if (minimum > maximum)
        {
            var rejected = new WorkerProtocolSelection(false, null, ErrorCodes.ProtocolVersionUnsupported);
            return new HandshakeResult(rejected, null);
        }

        var selection = new WorkerProtocolSelection(true, maximum);
        var ready = new ReadyMessage(
            hello.RequestId,
            hello.InstanceId,
            hello.WorkerSessionId,
            selection,
            _roots);
        return new HandshakeResult(selection, ready);
    }
}

using System.Runtime.Versioning;
using CfSharp;

namespace MirrorPulse.CloudFiles.CfSharp;

public interface IMirrorPulseAvailabilityPinOperations
{
    ValueTask<CloudAvailabilityChangeResult> SetAvailabilityAsync(
        CloudAvailabilityTarget target,
        CancellationToken cancellationToken);

    ValueTask<CloudStateChangeResult> SetPinStateAsync(
        CloudPinTarget target,
        CancellationToken cancellationToken);
}

public sealed record MirrorPulseAvailabilityPinResult(
    CloudAvailabilityChangeResult Availability,
    CloudStateChangeResult PinState);

/// <summary>
/// Applies availability and pin policy through one Cloud Files item boundary.
/// </summary>
[SupportedOSPlatform("windows10.0.16299")]
public sealed class MirrorPulseAvailabilityPinBridge
{
    private readonly IMirrorPulseAvailabilityPinOperations _operations;

    public MirrorPulseAvailabilityPinBridge(IMirrorPulseAvailabilityPinOperations operations)
    {
        ArgumentNullException.ThrowIfNull(operations);
        _operations = operations;
    }

    public static MirrorPulseAvailabilityPinBridge For(CloudFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return new(new CloudFileOperations(file));
    }

    public async ValueTask<MirrorPulseAvailabilityPinResult> ApplyAsync(
        CloudAvailabilityTarget availability,
        CloudPinTarget pinState,
        CancellationToken cancellationToken = default)
    {
        var availabilityResult = await _operations
            .SetAvailabilityAsync(availability, cancellationToken)
            .ConfigureAwait(false);
        var pinResult = await _operations
            .SetPinStateAsync(pinState, cancellationToken)
            .ConfigureAwait(false);
        return new(availabilityResult, pinResult);
    }

    private sealed class CloudFileOperations : IMirrorPulseAvailabilityPinOperations
    {
        private readonly CloudFile _file;

        public CloudFileOperations(CloudFile file) => _file = file;

        public ValueTask<CloudAvailabilityChangeResult> SetAvailabilityAsync(
            CloudAvailabilityTarget target,
            CancellationToken cancellationToken) =>
            _file.SetAvailabilityAsync(target, cancellationToken);

        public ValueTask<CloudStateChangeResult> SetPinStateAsync(
            CloudPinTarget target,
            CancellationToken cancellationToken) =>
            _file.SetPinStateAsync(target, cancellationToken);
    }
}

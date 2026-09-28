using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseAvailabilityPinBridgeTests
{
    [TestMethod]
    public async Task ApplyForwardsAvailabilityThenPinPolicy()
    {
        var operations = new RecordingOperations();
        var bridge = new MirrorPulseAvailabilityPinBridge(operations);

        var result = await bridge.ApplyAsync(
            CloudAvailabilityTarget.LocallyAvailable,
            CloudPinTarget.Pinned);

        Assert.AreEqual(CloudAvailabilityTarget.LocallyAvailable, operations.Availability);
        Assert.AreEqual(CloudPinTarget.Pinned, operations.PinState);
        Assert.IsNull(result.Availability);
        Assert.IsNull(result.PinState);
    }

    private sealed class RecordingOperations : IMirrorPulseAvailabilityPinOperations
    {
        public CloudAvailabilityTarget? Availability { get; private set; }
        public CloudPinTarget? PinState { get; private set; }

        public ValueTask<CloudAvailabilityChangeResult> SetAvailabilityAsync(
            CloudAvailabilityTarget target,
            CancellationToken cancellationToken)
        {
            Availability = target;
            return ValueTask.FromResult<CloudAvailabilityChangeResult>(default!);
        }

        public ValueTask<CloudStateChangeResult> SetPinStateAsync(
            CloudPinTarget target,
            CancellationToken cancellationToken)
        {
            PinState = target;
            return ValueTask.FromResult<CloudStateChangeResult>(default!);
        }
    }
}

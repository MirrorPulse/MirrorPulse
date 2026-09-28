using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.Core.CloudFiles;

namespace MirrorPulse.Core.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulseRemoteBatchApplierTests
{
    [TestMethod]
    public async Task ApplyForwardsBatchAndOptionsAndReturnsOutcome()
    {
        CloudRemoteChangeBatch? receivedBatch = null;
        CloudRemoteApplyOptions? receivedOptions = null;
        var expected = new MirrorPulseRemoteBatchOutcome(
            CloudRemoteBatchStatus.Applied,
            new byte[] { 8 },
            false,
            2,
            [Guid.NewGuid()]);
        var applier = new MirrorPulseRemoteBatchApplier((batch, options, _) =>
        {
            receivedBatch = batch;
            receivedOptions = options;
            return ValueTask.FromResult(expected);
        });
        var batch = new CloudRemoteChangeBatch("batch-1", new byte[] { 1 }, [], new byte[] { 2 });
        var options = new CloudRemoteApplyOptions { MaximumEntries = 10 };

        var actual = await applier.ApplyAsync(batch, options);

        Assert.AreSame(batch, receivedBatch);
        Assert.AreSame(options, receivedOptions);
        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public async Task ApplyUsesDefaultOptionsWhenOmitted()
    {
        CloudRemoteApplyOptions? receivedOptions = null;
        var applier = new MirrorPulseRemoteBatchApplier((_, options, _) =>
        {
            receivedOptions = options;
            return ValueTask.FromResult<MirrorPulseRemoteBatchOutcome>(default!);
        });

        await applier.ApplyAsync(new CloudRemoteChangeBatch(
            "batch-2",
            ReadOnlyMemory<byte>.Empty,
            Array.Empty<CloudRemoteChange>(),
            ReadOnlyMemory<byte>.Empty));

        Assert.AreSame(CloudRemoteApplyOptions.Default, receivedOptions);
    }
}

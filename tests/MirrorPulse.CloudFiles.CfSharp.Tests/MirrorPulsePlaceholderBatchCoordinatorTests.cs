using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.CloudFiles;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.16299")]
[TestClass]
public sealed class MirrorPulsePlaceholderBatchCoordinatorTests
{
    [TestMethod]
    public void PlanBuildsStableFileAndDirectoryPlaceholdersAndIgnoresDeletes()
    {
        var instance = InstanceId.New();
        var entries = new[]
        {
            new CloudRemoteDirectoryEntry(
                "remote-file", "revision-1", CloudItemKind.File, "Documents/report.pdf", null, 42,
                CloudPlaceholderMetadata.CreateFileBuilder().Build(), false),
            new CloudRemoteDirectoryEntry(
                "remote-directory", "revision-2", CloudItemKind.Directory, "Documents", null, null,
                CloudPlaceholderMetadata.CreateDirectoryBuilder().Build(), false),
            new CloudRemoteDirectoryEntry(
                "remote-deleted", "revision-3", CloudItemKind.File, "old.txt", null, 1,
                CloudPlaceholderMetadata.CreateFileBuilder().Build(), true),
        };

        var plan = MirrorPulsePlaceholderBatchCoordinator.Plan(instance, entries);

        Assert.HasCount(2, plan.Placeholders);
        Assert.HasCount(1, plan.IgnoredEntries);
        Assert.AreEqual("report.pdf", plan.Placeholders[0].Name);
        Assert.AreEqual(CloudItemKind.File, plan.Placeholders[0].Kind);
        Assert.AreEqual(CloudItemKind.Directory, plan.Placeholders[1].Kind);
        Assert.AreEqual(
            MirrorPulsePlaceholderIdentity.Create(instance, "remote-file", "revision-1").ToCfSharp().ItemId,
            plan.Placeholders[0].Identity.ItemId);
    }

    [TestMethod]
    public async Task CreateForwardsPlanAndOptionsToCloudFilesDirectory()
    {
        IReadOnlyList<CloudPlaceholderSpec>? received = null;
        CloudPlaceholderBatchOptions? receivedOptions = null;
        var coordinator = new MirrorPulsePlaceholderBatchCoordinator((placeholders, options, _) =>
        {
            received = placeholders;
            receivedOptions = options;
            return ValueTask.FromResult<CloudPlaceholderBatchResult>(default!);
        });
        var options = new CloudPlaceholderBatchOptions(stopOnFirstFailure: true);
        var entry = new CloudRemoteDirectoryEntry(
            "remote", "revision", CloudItemKind.File, "file.txt", null, 1,
            CloudPlaceholderMetadata.CreateFileBuilder().Build(), false);

        var result = await coordinator.CreateAsync(InstanceId.New(), [entry], options);

        Assert.IsNotNull(received);
        Assert.HasCount(1, received);
        Assert.AreSame(options, receivedOptions);
        Assert.IsNull(result);
    }
}

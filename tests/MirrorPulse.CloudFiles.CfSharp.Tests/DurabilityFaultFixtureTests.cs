using System.Diagnostics;
using System.Runtime.Versioning;
using CfSharp;
using MirrorPulse.CfSharp.CrashProbe;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;
using MirrorPulse.Core.State;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class DurabilityFaultFixtureTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (DurabilityBoundary boundary in Enum.GetValues<DurabilityBoundary>())
            foreach (FaultTiming timing in Enum.GetValues<FaultTiming>())
                foreach (FaultMode mode in Enum.GetValues<FaultMode>())
                    yield return [boundary, timing, mode];
    }

    [TestMethod]
    [DynamicData(nameof(Cases))]
    [TestCategory("DurabilityFaultFixture")]
    public async Task InjectedFailureLeavesTheExpectedDurableBoundary(
        DurabilityBoundary boundary, FaultTiming timing, FaultMode mode)
    {
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-fault-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, ".mp-fault-fixture"), "test-only");
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        try
        {
            var start = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[] { typeof(ProbeMarker).Assembly.Location, root, boundary.ToString(), timing.ToString(), mode.ToString() })
                start.ArgumentList.Add(argument);
            using Process process = Process.Start(start) ?? throw new InvalidOperationException("The fault probe did not start.");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            Task<string> output = process.StandardOutput.ReadToEndAsync(deadline.Token);
            Task<string> error = process.StandardError.ReadToEndAsync(deadline.Token);
            try { await process.WaitForExitAsync(deadline.Token); }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync();
                }
            }

            Assert.AreEqual(mode == FaultMode.Exit ? DurabilityFaultProbe.ExitFaultCode : DurabilityFaultProbe.ExceptionFaultCode,
                process.ExitCode, $"{await output}\n{await error}");
            string expected = timing == FaultTiming.Before ? "before" : "after";
            switch (boundary)
            {
                case DurabilityBoundary.RemoteWrite:
                    Assert.AreEqual(expected, await File.ReadAllTextAsync(Path.Combine(root, "remote", "document.txt")));
                    Assert.HasCount(1, Directory.GetFiles(Path.Combine(root, "remote")));
                    break;
                case DurabilityBoundary.JournalAcknowledgement:
                    await using (ICloudStateStore store = await MirrorPulseCfSharpStateStoreFactory.Create(paths)
                        .OpenAsync(new CloudStateStoreContext(paths.SyncRootPath)))
                    {
                        await using ICloudStateTransaction read = await store.BeginTransactionAsync();
                        CloudOperationJournalEntry? pending = await read.Operations.GetAsync(DurabilityFaultProbe.OperationId);
                        Assert.AreEqual(timing == FaultTiming.Before, pending is not null);
                        await read.RollbackAsync();
                    }
                    break;
                case DurabilityBoundary.SnapshotSave:
                    var snapshots = new MirrorPulseFileRemotePollSnapshotStore(paths.DataRootPath);
                    IReadOnlyDictionary<string, MirrorPulseRemoteSnapshotEntry>? snapshot = await snapshots.LoadAsync(DurabilityFaultProbe.Instance);
                    Assert.IsNotNull(snapshot);
                    Assert.AreEqual(expected, snapshot["document"].RemoteRevision);
                    Assert.HasCount(1, Directory.GetFiles(Path.Combine(paths.DataRootPath, "remote-poll")));
                    break;
                case DurabilityBoundary.CatalogSave:
                    await using (MirrorPulseProductCatalog catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
                    {
                        MirrorPulseUserCommandRecord? command = await catalog.ReadUserCommandAsync(DurabilityFaultProbe.OperationId);
                        Assert.IsNotNull(command);
                        Assert.AreEqual(expected, command.State);
                    }
                    break;
                case DurabilityBoundary.PendingRemoteBatch:
                    await using (MirrorPulseProductCatalog catalog = await MirrorPulseProductCatalog.OpenAsync(paths))
                    {
                        var pendingStore = new MirrorPulseCatalogRemotePollPendingStore(catalog);
                        MirrorPulsePendingRemotePoll? pending = await pendingStore.LoadAsync(DurabilityFaultProbe.Instance, CancellationToken.None);
                        Assert.AreEqual(timing == FaultTiming.After, pending is not null);
                        if (pending is not null)
                        {
                            Assert.AreEqual(DurabilityFaultProbe.Instance + "/fixture", pending.BatchId);
                            Assert.AreEqual("before", pending.Previous["document"].RemoteRevision);
                            Assert.AreEqual("after", pending.Candidate["document"].RemoteRevision);
                            CollectionAssert.AreEqual(new byte[32], pending.Fingerprint);
                        }
                    }
                    break;
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

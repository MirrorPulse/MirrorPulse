using MirrorPulse.Core.Conflicts;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Host;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class MirrorPulseAppStatusPipeTests
{
    [TestMethod]
    public async Task CurrentUserAppReceivesBoundedHostStatus()
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        Guid conflictId = Guid.NewGuid();
        var instanceId = InstanceId.New();
        var installId = InstallId.New();
        bool? selectedEnabled = null;
        InstallId? selectedInstallation = null;
        string? installedPackage = null;
        MirrorPulseCreateInstanceRequest? created = null;
        MirrorPulseConflictAction? resolvedAction = null;
        var expected = new MirrorPulseAppStatusResponse(3, 1,
            [new("instance-1", "Personal drive", true, "Healthy", "ABCDEF012345", null, null)],
            [new(conflictId.ToString("D"), "note.txt", DateTimeOffset.UtcNow, false,
                MirrorPulseConflictSource.Upload)], PendingUploadConflicts: 1);
        var server = new MirrorPulseAppStatusPipe(_ => Task.FromResult(expected),
            (id, _) => Task.FromResult(expected with
            {
                Notifications = [new(id.ToString("D"), "note.txt", DateTimeOffset.UtcNow, true)],
            }),
            (id, enabled, _) =>
            {
                Assert.AreEqual(instanceId, id);
                selectedEnabled = enabled;
                return Task.FromResult(expected);
            },
            (id, install, _) =>
            {
                Assert.AreEqual(instanceId, id);
                selectedInstallation = install;
                return Task.FromResult(expected);
            },
            (path, _) =>
            {
                installedPackage = path;
                return Task.FromResult(expected with { InstalledAdapterId = installId.ToString() });
            },
            (request, _) =>
            {
                created = request;
                return Task.FromResult(expected with { CreatedInstanceId = instanceId.ToString() });
            },
            (id, action, _) =>
            {
                Assert.AreEqual(conflictId, id);
                resolvedAction = action;
                return Task.FromResult(expected with { PendingUploadConflicts = 0 });
            });
        Task serving = server.ServeAsync(shutdown.Token);
        try
        {
            MirrorPulseAppStatusResponse received = await MirrorPulseAppStatusPipe.RequestAsync(shutdown.Token);
            Assert.AreEqual(3, received.PendingUploads);
            Assert.AreEqual(1, received.PendingRemoteConflicts);
            Assert.AreEqual(1, received.PendingUploadConflicts);
            Assert.AreEqual(MirrorPulseConflictSource.Upload, received.Notifications[0].Source);
            Assert.HasCount(1, received.Instances);
            Assert.AreEqual("Personal drive", received.Instances[0].DisplayName);
            MirrorPulseAppStatusResponse snoozed = await MirrorPulseAppStatusPipe.SnoozeAsync(conflictId,
                shutdown.Token);
            Assert.IsTrue(snoozed.Notifications[0].Snoozed);
            MirrorPulseAppStatusResponse resolved = await MirrorPulseAppStatusPipe.ResolveConflictAsync(
                conflictId, MirrorPulseConflictAction.KeepLocal, shutdown.Token);
            Assert.AreEqual(0, resolved.PendingUploadConflicts);
            Assert.AreEqual(MirrorPulseConflictAction.KeepLocal, resolvedAction);
            await MirrorPulseAppStatusPipe.SetInstanceEnabledAsync(instanceId, false, shutdown.Token);
            await MirrorPulseAppStatusPipe.SelectInstallationAsync(instanceId, installId, shutdown.Token);
            string packagePath = Path.Combine(Path.GetTempPath(), "MirrorPulse-status-test.mpadapter");
            MirrorPulseAppStatusResponse installed = await MirrorPulseAppStatusPipe.InstallAsync(
                packagePath, shutdown.Token);
            Assert.AreEqual(installId.ToString(), installed.InstalledAdapterId);
            var request = new MirrorPulseCreateInstanceRequest(installId.ToString(), "My source",
                new Dictionary<string, string> { ["sourceDirectory"] = @"C:\source" },
                new Dictionary<string, string> { ["local"] = "My source" }, null, true);
            MirrorPulseAppStatusResponse createdResponse = await MirrorPulseAppStatusPipe
                .CreateInstanceAsync(request, shutdown.Token);
            Assert.AreEqual(instanceId.ToString(), createdResponse.CreatedInstanceId);
            Assert.IsNotNull(created);
            Assert.AreEqual(@"C:\source", created.Configuration["sourceDirectory"]);
            Assert.AreEqual("My source", created.RootLabels["local"]);
            Assert.IsNotNull(selectedEnabled);
            Assert.IsFalse(selectedEnabled.Value);
            Assert.AreEqual(installId, selectedInstallation);
            Assert.AreEqual(Path.GetFullPath(packagePath), installedPackage);
        }
        finally
        {
            await shutdown.CancelAsync();
            await serving;
        }
    }
}

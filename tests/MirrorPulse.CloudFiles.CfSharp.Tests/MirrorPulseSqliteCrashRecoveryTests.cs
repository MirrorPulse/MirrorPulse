using System.Diagnostics;
using System.Runtime.Versioning;
using CfSharp;
using Microsoft.Data.Sqlite;
using MirrorPulse.CfSharp.CrashProbe;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[SupportedOSPlatform("windows10.0.19041")]
[TestClass]
public sealed class MirrorPulseSqliteCrashRecoveryTests
{
    [TestMethod]
    public async Task NativeOfficialStoreRecoversCommittedRepositoriesAndRollsBackInterruptedWrite()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
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
            start.ArgumentList.Add(typeof(ProbeMarker).Assembly.Location);
            start.ArgumentList.Add(root);
            using (Process process = Process.Start(start)
                ?? throw new InvalidOperationException("The SQLite crash probe did not start."))
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                Task<string> output = process.StandardOutput.ReadToEndAsync(timeout.Token);
                Task<string> error = process.StandardError.ReadToEndAsync(timeout.Token);
                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                finally
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        await process.WaitForExitAsync();
                    }
                }

                Assert.AreEqual(37, process.ExitCode, $"{await output}\n{await error}");
            }

            Assert.IsTrue(File.Exists(paths.CfSharpStateDatabasePath));
            await using (ICloudStateStore reopened = await MirrorPulseCfSharpStateStoreFactory.Create(paths)
                .OpenAsync(new CloudStateStoreContext(paths.SyncRootPath)))
            {
                await using ICloudStateTransaction read = await reopened.BeginTransactionAsync();
                Assert.IsNotNull(await read.Operations.GetAsync(
                    new Guid("95a37be7-6b27-42c4-9006-526ddda5e856")));
                CloudRemoteBatchState? partial = await read.RemoteBatches.GetAsync("partial");
                Assert.IsNotNull(partial);
                Assert.AreEqual(CloudRemoteBatchStatus.Applying, partial.Status);
                Assert.AreEqual(1, partial.AppliedEntryCount);
                Assert.IsNotNull(await read.Conflicts.GetAsync(
                    new Guid("d0de7d17-2624-41ec-a457-a86f38e71d16")));
                Assert.IsNotNull(await read.EchoSuppressions.GetAsync(
                    new Guid("c5a089f7-97a0-4251-aad3-8665b1cb0025")));
                CloudStateCheckpoint? checkpoint = await read.Checkpoints.GetAsync("remote/instance");
                CollectionAssert.AreEqual(new byte[] { 2 }, checkpoint?.Value.ToArray());
                Assert.IsNull(await read.Checkpoints.GetAsync("must-rollback"));
                await read.RollbackAsync();
            }

            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = paths.CfSharpStateDatabasePath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            };
            await using var sqlite = new SqliteConnection(builder.ToString());
            await sqlite.OpenAsync();
            // SQLite needs the provider's index collation when checking its schema.
            sqlite.CreateCollation("CFSHARP_UNICODE_NOCASE",
                static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left, right));
            await using var integrity = sqlite.CreateCommand();
            integrity.CommandText = "PRAGMA integrity_check;";
            Assert.AreEqual("ok", await integrity.ExecuteScalarAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}

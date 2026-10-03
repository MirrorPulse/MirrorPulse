using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CfSharp;
using CfSharp.Native;
using Microsoft.Win32.SafeHandles;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
[SupportedOSPlatform("windows10.0.19041")]
public sealed partial class MirrorPulseConditionalInSyncTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    public async Task IndependentFileUsnAcceptsCurrentRejectsStaleAndAcceptsRefreshedToken()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
            Assert.Inconclusive("Requires the disposable NativeCloudFiles verification environment.");
        string root = Path.Combine(Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        var paths = new MirrorPulseStoragePaths(Path.Combine(root, "sync"), Path.Combine(root, "data"));
        var registry = new CfSharpMirrorPulseCloudRootRegistry();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            Directory.CreateDirectory(paths.SyncRootPath);
            registry.Register(new(paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]));
            var state = new MirrorPulseCfSharpStateSession(paths);
            await using var fileSystem = new MirrorPulseCloudFileSystemBuilder(paths).WithStateStore(state)
                .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath)).Build();
            await fileSystem.StartAsync(timeout.Token);
            string path = Path.Combine(paths.SyncRootPath, "conditional.txt");
            await File.WriteAllTextAsync(path, "before", timeout.Token);
            CloudFile file = fileSystem.GetFile("conditional.txt");
            CloudPlaceholderMutationResult converted = await file.ConvertToPlaceholderAsync(
                new(Guid.NewGuid(), "probe", "probe-revision"), cancellationToken: timeout.Token);
            await file.SetInSyncAsync(false, cancellationToken: timeout.Token);
            TestContext.WriteLine($"Conditional USN environment: CfSharp=0.1.0-preview.2; OS={Environment.OSVersion.Version}; architecture={RuntimeInformation.ProcessArchitecture}; convertOutput={converted.OperationUsn}.");

            long current = ReadFileUsn(path);
            bool currentAccepted = await TryMarkAsync(file, "current", current, timeout.Token);
            CloudSynchronizationState currentState = (await file.InspectAsync(timeout.Token)).SynchronizationState;
            TestContext.WriteLine($"Conditional USN current state: {currentState}.");
            TestContext.WriteLine($"Conditional USN after public mark: before=0x{current:X16}; after=0x{ReadFileUsn(path):X16}.");
            ReportSameHandleMark(path);

            await file.SetInSyncAsync(false, cancellationToken: timeout.Token);
            long stale = ReadFileUsn(path);
            // A same-length write closes its handle before the query: length alone cannot
            // detect this edit, and no pending writable handle can coalesce a later write.
            await using (var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Read))
            {
                await writer.WriteAsync("after!"u8.ToArray(), timeout.Token);
                await writer.FlushAsync(timeout.Token);
            }
            long changed = ReadFileUsn(path);
            CloudSynchronizationState editedState = (await file.InspectAsync(timeout.Token)).SynchronizationState;
            TestContext.WriteLine($"Conditional USN edit: before=0x{stale:X16}; after=0x{changed:X16}.");
            TestContext.WriteLine($"Conditional USN edited state: {editedState}.");
            bool staleAccepted = await TryMarkAsync(file, "stale", stale, timeout.Token);
            CloudSynchronizationState rejectedState = (await file.InspectAsync(timeout.Token)).SynchronizationState;
            string preserved = await File.ReadAllTextAsync(path, timeout.Token);
            TestContext.WriteLine($"Conditional USN stale state: {rejectedState}; contentPreserved={preserved == "after!"}.");

            await file.SetInSyncAsync(false, cancellationToken: timeout.Token);
            long refreshed = ReadFileUsn(path);
            bool refreshedAccepted = await TryMarkAsync(file, "refreshed", refreshed, timeout.Token);
            CloudSynchronizationState refreshedState = (await file.InspectAsync(timeout.Token)).SynchronizationState;
            TestContext.WriteLine($"Conditional USN refreshed state: {refreshedState}.");

            Assert.IsTrue(currentAccepted, "A separately queried current USN must be accepted.");
            Assert.AreEqual(CloudSynchronizationState.InSync, currentState);
            Assert.AreNotEqual(stale, changed, "The closed same-length write must advance the file USN.");
            Assert.AreEqual(CloudSynchronizationState.NotInSync, editedState, "The in-place edit must preserve placeholder identity.");
            Assert.IsFalse(staleAccepted, "The old USN must not confirm edited content.");
            Assert.AreEqual(CloudSynchronizationState.NotInSync, rejectedState);
            Assert.AreEqual("after!", preserved, "A rejected conditional mark must preserve edited bytes.");
            Assert.IsTrue(refreshedAccepted, "A refreshed USN must be accepted after stale rejection.");
            Assert.AreEqual(CloudSynchronizationState.InSync, refreshedState);
        }
        finally
        {
            registry.Unregister(paths.SyncRootPath);
            Directory.Delete(root, recursive: true);
        }
    }

    private async Task<bool> TryMarkAsync(CloudFile file, string phase, long usn, CancellationToken token)
    {
        try
        {
            CloudStateChangeResult result = await file.SetInSyncAsync(true, new CloudInSyncChangeOptions(usn), token);
            TestContext.WriteLine($"Conditional USN {phase}: input=0x{usn:X16}; accepted=True; output={result.OperationUsn}.");
            // The native output may still be zero. Acceptance and the inspected state,
            // rather than a fabricated positive output, are the observations under test.
            return true;
        }
        catch (CloudFilesException exception)
        {
            TestContext.WriteLine($"Conditional USN {phase}: input=0x{usn:X16}; accepted=False; operation={exception.Operation}; HRESULT=0x{exception.HResult:X8}; Win32={exception.Win32ErrorCode}.");
            return false;
        }
    }

    private static unsafe long ReadFileUsn(string path)
    {
        // Test-only Windows query, independent of CfSharp mutation outputs. Request the
        // documented default V2 record on this NTFS fixture; never read a volume journal.
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ReadFileUsn(handle);
    }

    private void ReportSameHandleMark(string path)
    {
        var result = MarkSameHandle(path);
        TestContext.WriteLine($"Conditional USN same-handle native: firstRead=0x{result.FirstRead:X16}; input=0x{result.Input:X16}; HRESULT=0x{result.HResult:X8}; output={result.Output}; after=0x{result.After:X16}.");
    }

    private static unsafe (long FirstRead, long Input, int HResult, long Output, long After) MarkSameHandle(string path)
    {
        // Diagnostic only: remove the public method's second path open from the comparison.
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete);
        long firstRead = ReadFileUsn(handle);
        long input = ReadFileUsn(handle);
        long output = input;
        int hresult = CfApi.CfSetInSyncState(handle.DangerousGetHandle(), CfInSyncState.InSync, CfSetInSyncFlags.None, &output);
        return (firstRead, input, hresult, output, ReadFileUsn(handle));
    }

    private static unsafe long ReadFileUsn(SafeFileHandle handle)
    {
        Span<byte> buffer = stackalloc byte[1024];
        uint returned;
        fixed (byte* output = buffer)
        {
            if (DeviceIoControl(handle, 0x000900EB, null, 0, output, (uint)buffer.Length, &returned, null) == 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
        Assert.IsTrue(returned >= 60 && returned <= buffer.Length, "Windows must return a complete USN_RECORD_V2.");
        Assert.AreEqual((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..]));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        Assert.IsTrue(length >= 60 && length <= returned, "The file USN record must fit its returned buffer.");
        long usn = BinaryPrimitives.ReadInt64LittleEndian(buffer[24..]);
        Assert.IsGreaterThan(0L, usn, "The disposable NTFS file must have a positive current USN.");
        return usn;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int DeviceIoControl(SafeFileHandle device, uint code, void* input, uint inputLength,
        void* output, uint outputLength, uint* returned, void* overlapped);
}

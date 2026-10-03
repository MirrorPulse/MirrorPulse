using System.ComponentModel;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using CfSharp;
using CfSharp.Native;
using Microsoft.Win32.SafeHandles;
using MirrorPulse.CfSharp.CrashProbe;
using MirrorPulse.CloudFiles.CfSharp;
using MirrorPulse.Core.Configuration;

namespace MirrorPulse.CloudFiles.CfSharp.Tests;

[TestClass]
[SupportedOSPlatform("windows10.0.19041")]
public sealed partial class MirrorPulseProtectedConfirmationTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    [DataRow(0)]
    [DataRow(6)]
    [DataRow(131072)]
    public async Task ExclusiveReferenceConfirmsCompleteContentOnTheSameHandle(int length)
    {
        RequireNativeFixture();
        byte[] bytes = new byte[length];
        new Random(8192).NextBytes(bytes);
        await using Fixture fixture = await Fixture.CreateAsync(bytes);
        var watch = Stopwatch.StartNew();
        using (var lease = ProtectedReference.Open(fixture.Path))
        {
            Observation before = lease.Inspect();
            Assert.AreEqual(CfInSyncState.NotInSync, before.State);
            Assert.IsTrue(lease.VerifyAndConfirm(before.FileId, fixture.Identity.Encode(), length, SHA256.HashData(bytes)));
            Observation after = lease.Inspect();
            Assert.AreEqual(CfInSyncState.InSync, after.State);
            Assert.AreEqual(before.FileId, after.FileId);
            CollectionAssert.AreEqual(fixture.Identity.Encode(), after.Identity);
        }
        Assert.AreEqual(CloudSynchronizationState.InSync, (await fixture.File.InspectAsync()).SynchronizationState);
        CollectionAssert.AreEqual(bytes, await File.ReadAllBytesAsync(fixture.Path));
        TestContext.WriteLine($"Protected confirmation basic: length={length}; confirmed=True; sameIdentity=True; elapsedMs={watch.ElapsedMilliseconds}; OS={Environment.OSVersion.Version}; architecture={RuntimeInformation.ProcessArchitecture}; CfSharp=0.1.0-preview.2.");
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    [DataRow("write")]
    [DataRow("rename")]
    [DataRow("replace")]
    public async Task ExclusiveReferenceSerializesACompetingProcessUntilConfirmationFinishes(string mode)
    {
        RequireNativeFixture();
        await using Fixture fixture = await Fixture.CreateAsync("before"u8.ToArray());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var lease = ProtectedReference.Open(fixture.Path);
        Observation original = lease.Inspect();
        using Process writer = StartWriter(fixture.Root, mode);
        Task<string> error = writer.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            Assert.AreEqual("attempt", await writer.StandardOutput.ReadLineAsync(timeout.Token));
            await Task.Delay(250, timeout.Token);
            Assert.IsFalse(writer.HasExited, "A competing opener must wait while the protected reference is held.");
            Assert.IsTrue(lease.VerifyAndConfirm(original.FileId, fixture.Identity.Encode(), 6, SHA256.HashData("before"u8)));
            Assert.AreEqual(CfInSyncState.InSync, lease.Inspect().State);
            lease.Dispose();
            await writer.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, writer.ExitCode, await error);
            Assert.AreEqual("completed", await writer.StandardOutput.ReadLineAsync(timeout.Token));
            if (mode == "write")
            {
                Assert.AreEqual("after!", await File.ReadAllTextAsync(fixture.Path, timeout.Token));
                Assert.AreEqual(CloudSynchronizationState.NotInSync, (await fixture.File.InspectAsync(timeout.Token)).SynchronizationState);
            }
            else if (mode == "rename")
            {
                Assert.IsFalse(File.Exists(fixture.Path));
                Assert.AreEqual("before", await File.ReadAllTextAsync(fixture.Path + ".moved", timeout.Token));
            }
            else
            {
                Assert.AreEqual("after!", await File.ReadAllTextAsync(fixture.Path, timeout.Token));
                Assert.IsFalse((await fixture.File.InspectAsync(timeout.Token)).IsPlaceholder,
                    "The replacement must not inherit the old file's confirmation.");
            }
            TestContext.WriteLine($"Protected confirmation competitor: mode={mode}; waited=True; confirmedBeforeRelease=True; completedAfterRelease=True; replacementNotConfirmed=True.");
        }
        finally
        {
            lease.Dispose();
            await StopWriterAsync(writer);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    [DataRow("hash")]
    [DataRow("length")]
    [DataRow("identity")]
    [DataRow("fileId")]
    [DataRow("cancel")]
    [DataRow("fault")]
    public async Task RejectedVerificationAndInterruptedConfirmationLeaveContentPendingAndReleaseTheHandle(string mode)
    {
        RequireNativeFixture();
        await using Fixture fixture = await Fixture.CreateAsync("before"u8.ToArray());
        using var cancellation = new CancellationTokenSource();
        using (var lease = ProtectedReference.Open(fixture.Path))
        {
            long fileId = lease.Inspect().FileId;
            byte[] identity = fixture.Identity.Encode();
            byte[] hash = SHA256.HashData("before"u8);
            long length = 6;
            if (mode == "hash") hash = SHA256.HashData("after!"u8);
            if (mode == "length") length++;
            if (mode == "identity") identity = new CloudPlaceholderIdentity(fixture.Identity.ItemId, "probe", "other-revision").Encode();
            if (mode == "fileId") fileId++;
            Action? beforeConfirm = mode switch
            {
                "cancel" => cancellation.Cancel,
                "fault" => () => throw new InjectedConfirmationException(),
                _ => null,
            };
            if (mode == "cancel")
                Assert.ThrowsExactly<OperationCanceledException>(() => lease.VerifyAndConfirm(fileId, identity, length, hash, beforeConfirm, cancellation.Token));
            else if (mode == "fault")
                Assert.ThrowsExactly<InjectedConfirmationException>(() => lease.VerifyAndConfirm(fileId, identity, length, hash, beforeConfirm, cancellation.Token));
            else Assert.IsFalse(lease.VerifyAndConfirm(fileId, identity, length, hash));
            Assert.AreEqual(CfInSyncState.NotInSync, lease.Inspect().State);
        }
        using (var reopened = ProtectedReference.Open(fixture.Path)) Assert.AreEqual(CfInSyncState.NotInSync, reopened.Inspect().State);
        Assert.AreEqual("before", await File.ReadAllTextAsync(fixture.Path));
        TestContext.WriteLine($"Protected confirmation refusal: mode={mode}; remainsPending=True; contentPreserved=True; handleReopened=True.");
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingWritableHandleOrMappingPreventsAnExclusiveConfirmation(bool mapped)
    {
        RequireNativeFixture();
        await using Fixture fixture = await Fixture.CreateAsync("before"u8.ToArray());
        int hresult;
        using (var writer = new FileStream(fixture.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
        {
            using MemoryMappedFile? mapping = mapped ? MemoryMappedFile.CreateFromFile(writer, null, 0,
                MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: true) : null;
            using MemoryMappedViewAccessor? view = mapping?.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
            if (mapped) writer.Dispose();
            NativeProbeException rejected = Assert.ThrowsExactly<NativeProbeException>(() =>
            {
                using var unexpected = ProtectedReference.Open(fixture.Path);
                Assert.Fail("An existing writable handle or view must prevent exclusive confirmation.");
            });
            hresult = rejected.HResult;
        }
        using (var reopened = ProtectedReference.Open(fixture.Path)) Assert.AreEqual(CfInSyncState.NotInSync, reopened.Inspect().State);
        TestContext.WriteLine($"Protected confirmation existing writer: mapped={mapped}; rejected=True; HRESULT=0x{hresult:X8}; handleReopened=True; remainsPending=True.");
    }

    [TestMethod]
    [DoNotParallelize]
    [TestCategory("NativeCloudFiles")]
    public async Task BrokenOplockBetweenReadSegmentsPreventsFinalConfirmation()
    {
        RequireNativeFixture();
        await using Fixture fixture = await Fixture.CreateAsync("before"u8.ToArray());
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var lease = ProtectedReference.Open(fixture.Path);
        CollectionAssert.AreEqual("bef"u8.ToArray(), lease.ReadPrefix(3));
        using Process writer = StartWriter(fixture.Root, "write");
        Task<string> error = writer.StandardError.ReadToEndAsync(timeout.Token);
        try
        {
            Assert.AreEqual("attempt", await writer.StandardOutput.ReadLineAsync(timeout.Token));
            lease.ReleaseReference();
            await writer.WaitForExitAsync(timeout.Token);
            Assert.AreEqual(0, writer.ExitCode, await error);
            Assert.IsFalse(lease.TryReference(), "The invalidated protected handle must not resume a stale verification.");
            Assert.AreEqual("after!", await File.ReadAllTextAsync(fixture.Path, timeout.Token));
            Assert.AreEqual(CloudSynchronizationState.NotInSync, (await fixture.File.InspectAsync(timeout.Token)).SynchronizationState);
            TestContext.WriteLine("Protected confirmation segment break: writerCompleted=True; referenceRejected=True; noConfirmation=True; remainsPending=True.");
        }
        finally
        {
            lease.Dispose();
            await StopWriterAsync(writer);
        }
    }

    private static void RequireNativeFixture()
    {
        if (Environment.GetEnvironmentVariable("MIRRORPULSE_NATIVE_TEST") != "1")
            Assert.Inconclusive("Requires the disposable NativeCloudFiles verification environment.");
    }

    private static Process StartWriter(string root, string mode)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in new[] { typeof(ProbeMarker).Assembly.Location, "--protected-writer", root, mode }) start.ArgumentList.Add(argument);
        return Process.Start(start) ?? throw new InvalidOperationException("The competing writer did not start.");
    }

    private static async Task StopWriterAsync(Process writer)
    {
        if (!writer.HasExited) writer.Kill(entireProcessTree: true);
        await writer.WaitForExitAsync();
    }

    private sealed record Observation(long FileId, CfInSyncState State, byte[] Identity);

    private sealed class InjectedConfirmationException : IOException;

    private sealed class NativeProbeException : IOException
    {
        public NativeProbeException(string operation, int hresult) : base(operation) => HResult = hresult;
    }

    // Test-only prototype. The eventual product boundary must use a managed CfSharp API.
    private sealed class ProtectedReference : IDisposable
    {
        private readonly nint _protected;
        private SafeFileHandle? _borrowed;
        private bool _closed;
        private ProtectedReference(nint handle) => _protected = handle;

        public static unsafe ProtectedReference Open(string path)
        {
            int result;
            nint handle;
            fixed (char* pointer = path) result = CfApi.CfOpenFileWithOplock(pointer, CfOpenFileFlags.Exclusive | CfOpenFileFlags.WriteAccess, out handle);
            ThrowNative(result, "CfOpenFileWithOplock");
            var lease = new ProtectedReference(handle);
            try
            {
                if (!lease.TryReference()) throw new InvalidOperationException("The protected handle could not be referenced.");
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }

        public bool TryReference()
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_borrowed is not null) throw new InvalidOperationException("A protected reference is already held.");
            if (CfApi.CfReferenceProtectedHandle(_protected) == 0) return false;
            nint handle = CfApi.CfGetWin32HandleFromProtectedHandle(_protected);
            if (handle == 0 || handle == -1)
            {
                CfApi.CfReleaseProtectedHandle(_protected);
                throw new InvalidOperationException("Windows returned an invalid borrowed handle.");
            }
            _borrowed = new SafeFileHandle(handle, ownsHandle: false);
            return true;
        }

        public unsafe Observation Inspect()
        {
            Span<byte> buffer = stackalloc byte[8192];
            fixed (byte* pointer = buffer)
            {
                uint returned;
                ThrowNative(CfApi.CfGetPlaceholderInfo(Handle.DangerousGetHandle(), CfPlaceholderInfoClass.Standard,
                    pointer, (uint)buffer.Length, &returned), "CfGetPlaceholderInfo");
                var info = (CfPlaceholderStandardInfo*)pointer;
                int offset = Marshal.OffsetOf<CfPlaceholderStandardInfo>(nameof(CfPlaceholderStandardInfo.FileIdentity)).ToInt32();
                Assert.IsTrue(returned <= buffer.Length && returned >= offset && info->FileIdentityLength <= returned - offset);
                return new(info->FileId, info->InSyncState, buffer.Slice(offset, checked((int)info->FileIdentityLength)).ToArray());
            }
        }

        public bool VerifyAndConfirm(long fileId, byte[] identity, long length, byte[] expectedHash,
            Action? beforeConfirm = null, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            Observation initial = Inspect();
            if (initial.FileId != fileId || !initial.Identity.AsSpan().SequenceEqual(identity) || RandomAccess.GetLength(Handle) != length) return false;
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[4096];
            long offset = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int count = ReadBorrowedHandle(Handle, buffer, offset);
                if (count == 0) break;
                hash.AppendData(buffer, 0, count);
                offset += count;
            }
            if (offset != length || !CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(), expectedHash)) return false;
            beforeConfirm?.Invoke();
            token.ThrowIfCancellationRequested();
            Observation final = Inspect();
            if (final.FileId != fileId || !final.Identity.AsSpan().SequenceEqual(identity)) return false;
            MarkSameHandle();
            return true;
        }

        public byte[] ReadPrefix(int count)
        {
            byte[] bytes = new byte[count];
            Assert.AreEqual(count, ReadBorrowedHandle(Handle, bytes, 0));
            return bytes;
        }

        private unsafe void MarkSameHandle() => ThrowNative(CfApi.CfSetInSyncState(Handle.DangerousGetHandle(),
            CfInSyncState.InSync, CfSetInSyncFlags.None, null), "CfSetInSyncState");

        private SafeFileHandle Handle => _borrowed ?? throw new InvalidOperationException("A protected reference is required.");
        private static void ThrowNative(int hresult, string operation)
        {
            if (hresult < 0) throw new NativeProbeException(operation, hresult);
        }

        public void ReleaseReference()
        {
            if (_borrowed is null) return;
            _borrowed.Dispose();
            _borrowed = null;
            CfApi.CfReleaseProtectedHandle(_protected);
        }

        public void Dispose()
        {
            if (_closed) return;
            ReleaseReference();
            CfApi.CfCloseHandle(_protected);
            _closed = true;
        }
    }

    private static unsafe int ReadBorrowedHandle(SafeFileHandle handle, byte[] buffer, long offset)
    {
        // CfOpenFileWithOplock owns this asynchronous handle's completion-port binding.
        // A private event with its low bit set suppresses completion-port delivery for
        // our OVERLAPPED; drain it before freeing its buffer or releasing the reference.
        using var signal = new EventWaitHandle(false, EventResetMode.ManualReset);
        var overlapped = new NativeOverlapped
        {
            OffsetLow = unchecked((int)offset),
            OffsetHigh = unchecked((int)(offset >> 32)),
            EventHandle = signal.SafeWaitHandle.DangerousGetHandle() | 1,
        };
        fixed (byte* pointer = buffer)
        {
            if (ReadFile(handle, pointer, (uint)buffer.Length, null, &overlapped) == 0)
            {
                int error = Marshal.GetLastPInvokeError();
                if (error == 38) return 0;
                if (error != 997) throw new Win32Exception(error);
            }
            uint transferred;
            if (GetOverlappedResult(handle, &overlapped, &transferred, 1) == 0)
            {
                int error = Marshal.GetLastPInvokeError();
                if (error == 38) return 0;
                throw new Win32Exception(error);
            }
            return checked((int)transferred);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int ReadFile(SafeFileHandle file, void* buffer, uint length, uint* read, NativeOverlapped* overlapped);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static unsafe partial int GetOverlappedResult(SafeFileHandle file, NativeOverlapped* overlapped, uint* transferred, int wait);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly CfSharpMirrorPulseCloudRootRegistry _registry = new();
        private CloudFileSystem _fileSystem = null!;
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MirrorPulse-native-tests", Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Root, "sync", "confirmed.bin");
        public CloudPlaceholderIdentity Identity { get; } = new(Guid.NewGuid(), "probe", "probe-revision");
        public CloudFile File => _fileSystem.GetFile("confirmed.bin");

        public static async Task<Fixture> CreateAsync(byte[] bytes)
        {
            var fixture = new Fixture();
            try
            {
                var paths = new MirrorPulseStoragePaths(System.IO.Path.Combine(fixture.Root, "sync"), System.IO.Path.Combine(fixture.Root, "data"));
                Directory.CreateDirectory(paths.SyncRootPath);
                await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(fixture.Root, ".mp-protected-fixture"), "owned fixture");
                fixture._registry.Register(new(paths.SyncRootPath, "0.1.0", Guid.NewGuid(), [1, 2, 3]));
                var state = new MirrorPulseCfSharpStateSession(paths);
                fixture._fileSystem = new MirrorPulseCloudFileSystemBuilder(paths).WithStateStore(state)
                    .WithContentProvider(MirrorPulseDemandProvider.CreateWithoutAdapters(paths.SyncRootPath)).Build();
                await fixture._fileSystem.StartAsync();
                await System.IO.File.WriteAllBytesAsync(fixture.Path, bytes);
                await fixture.File.ConvertToPlaceholderAsync(fixture.Identity);
                await fixture.File.SetInSyncAsync(false);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            if (_fileSystem is not null) await _fileSystem.DisposeAsync();
            _registry.Unregister(System.IO.Path.Combine(Root, "sync"));
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}

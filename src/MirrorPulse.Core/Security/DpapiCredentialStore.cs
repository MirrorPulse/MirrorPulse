using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MirrorPulse.Core.Contracts;

namespace MirrorPulse.Core.Security;

public interface IDpapiProtector
{
    byte[] Protect(ReadOnlySpan<byte> value);

    byte[] Unprotect(ReadOnlySpan<byte> value);
}

/// <summary>
/// File-backed current-user credential store protected by Windows DPAPI.
/// </summary>
public sealed class DpapiCredentialStore : ISecureCredentialStore
{
    private readonly string _rootDirectory;
    private readonly IDpapiProtector _protector;

    public DpapiCredentialStore(string rootDirectory, IDpapiProtector? protector = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
        _protector = protector ?? new WindowsDpapiProtector();
    }

    public string RootDirectory => _rootDirectory;

    public async ValueTask SaveAsync(
        CredentialReference reference,
        ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        cancellationToken.ThrowIfCancellationRequested();
        var protectedValue = _protector.Protect(secret.Span);
        var path = GetPath(reference);
        var temporary = path + $".{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(_rootDirectory);
            await File.WriteAllBytesAsync(temporary, protectedValue, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedValue);
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public async ValueTask<SecureCredentialValue?> TryGetAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        var path = GetPath(reference);
        if (!File.Exists(path))
        {
            return null;
        }

        var protectedValue = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        try
        {
            var plainValue = _protector.Unprotect(protectedValue);
            try
            {
                return new SecureCredentialValue(reference, plainValue);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plainValue);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedValue);
        }
    }

    public ValueTask DeleteAsync(
        CredentialReference reference,
        CancellationToken cancellationToken = default)
    {
        ValidateReference(reference);
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(reference);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return ValueTask.CompletedTask;
    }

    public string GetPath(CredentialReference reference)
    {
        ValidateReference(reference);
        var identity = $"{reference.Provider}\n{reference.ReferenceId}";
        var digest = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(identity)));
        return Path.Combine(_rootDirectory, $"{digest}.cred");
    }

    private static void ValidateReference(CredentialReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (reference.Scope != CredentialScope.CurrentUser)
        {
            throw new ArgumentException("DPAPI credential storage is limited to the current user.", nameof(reference));
        }
    }

    private sealed class WindowsDpapiProtector : IDpapiProtector
    {
        private const uint UiForbidden = 1;

        public byte[] Protect(ReadOnlySpan<byte> value) => Transform(value, protect: true);

        public byte[] Unprotect(ReadOnlySpan<byte> value) => Transform(value, protect: false);

        private static byte[] Transform(ReadOnlySpan<byte> value, bool protect)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI credential storage is available only on Windows.");
            }

            var inputBytes = value.ToArray();
            var inputPointer = inputBytes.Length == 0 ? IntPtr.Zero : Marshal.AllocHGlobal(inputBytes.Length);
            try
            {
                if (inputPointer != IntPtr.Zero)
                {
                    Marshal.Copy(inputBytes, 0, inputPointer, inputBytes.Length);
                }

                var input = new DataBlob((uint)inputBytes.Length, inputPointer);
                var output = default(DataBlob);
                var success = protect
                    ? CryptProtectData(ref input, "MirrorPulse credential", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output)
                    : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, UiForbidden, ref output);
                if (!success)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "DPAPI transformation failed.");
                }

                try
                {
                    if (output.Size > int.MaxValue)
                    {
                        throw new InvalidDataException("The DPAPI result is too large.");
                    }

                    var result = new byte[(int)output.Size];
                    if (result.Length > 0)
                    {
                        Marshal.Copy(output.Data, result, 0, result.Length);
                    }

                    return result;
                }
                finally
                {
                    LocalFree(output.Data);
                }
            }
            finally
            {
                if (inputPointer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(inputPointer);
                }

                CryptographicOperations.ZeroMemory(inputBytes);
            }
        }

        [DllImport("Crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            ref DataBlob dataOut);

        [DllImport("Crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            uint flags,
            ref DataBlob dataOut);

        [DllImport("Kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob(uint size, IntPtr data)
        {
            public uint Size = size;
            public IntPtr Data = data;
        }
    }
}

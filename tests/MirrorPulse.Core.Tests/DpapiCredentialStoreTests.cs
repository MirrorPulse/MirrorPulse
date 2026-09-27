using System.Security.Cryptography;
using MirrorPulse.Core.Contracts;
using MirrorPulse.Core.Security;

namespace MirrorPulse.Core.Tests;

[TestClass]
public sealed class DpapiCredentialStoreTests
{
    [TestMethod]
    public async Task StoreProtectsCredentialBytesAndRoundTripsThem()
    {
        var root = Path.Combine(Path.GetTempPath(), $"mirrorpulse-dpapi-{Guid.NewGuid():N}");
        var reference = new CredentialReference("ref-1", CredentialKind.Password, "example", CredentialScope.CurrentUser, DateTimeOffset.UtcNow);
        try
        {
            var store = new DpapiCredentialStore(root, new XorProtector());
            await store.SaveAsync(reference, "secret"u8.ToArray());
            var path = store.GetPath(reference);
            using var value = await store.TryGetAsync(reference);

            Assert.IsNotNull(value);
            CollectionAssert.AreEqual("secret"u8.ToArray(), value.Value.ToArray());
            CollectionAssert.AreNotEqual("secret"u8.ToArray(), await File.ReadAllBytesAsync(path));
            await store.DeleteAsync(reference);
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class XorProtector : IDpapiProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> value) => Transform(value);

        public byte[] Unprotect(ReadOnlySpan<byte> value) => Transform(value);

        private static byte[] Transform(ReadOnlySpan<byte> value)
        {
            var result = value.ToArray();
            for (var index = 0; index < result.Length; index++)
            {
                result[index] ^= 0xA5;
            }

            return result;
        }
    }
}
